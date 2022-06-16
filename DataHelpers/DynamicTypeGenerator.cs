using drewCo.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Web;

namespace officepark.io.Data;

// ============================================================================================================================
public class DynamicTypeManager
{
  /// <summary>
  /// The current assembly builder.  It is (should be) possible to update + refresh our types during runtime.
  /// </summary>
  private AssemblyBuilder AsmBuilder = null;
  private ModuleBuilder ModBuilder = null;
  private List<Type> GeneratedTypes = null;
  private object AsmBuilderLock = new object();

  // NOTE: This should probably be joined onto the code for 'ReflectionTools.ResolveType'
  private object TypeResolverLock = new object();
  private Dictionary<string, Type> ResolvedTypes = new Dictionary<string, Type>()
        {
            {"int", typeof(int) },
            {"float", typeof(float) },
            {"double", typeof(double) },
            {"string", typeof(string) },
//            {"decimal", typeof(decimal) },
        };


  // --------------------------------------------------------------------------------------------------------------------------
  public DynamicTypeManager(string assemblyName)
  {
    AssemblyName = assemblyName;
  }

  public string AssemblyName { get; private set; }

  // --------------------------------------------------------------------------------------------------------------------------
  public object? CreateInstance(string typeName)
  {
    if (!ResolvedTypes.TryGetValue(typeName, out Type? t))
    {
      string msg = $"There is no resolved type with name {typeName}!";
      msg += Environment.NewLine + "Valid Names are: " + Environment.NewLine + string.Join(Environment.NewLine, ResolvedTypes.Keys);

      throw new KeyNotFoundException(msg);
    }

    object? res = Activator.CreateInstance(t);
    return res;
  }


  //// --------------------------------------------------------------------------------------------------------------------------
  //public void LoadDynamicTypesFromFile(string defsPath)
  //{
  //  // Make sure that we can load the defs:
  //  //TypeDefsFile defsFile = TypeDefsFile.Load(defsPath);

  //  lock (AsmBuilderLock)
  //  {
  //    if (AsmBuilder == null)
  //    {
  //      AsmBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(AssemblyName), AssemblyBuilderAccess.RunAndSave);

  //      ModBuilder = AsmBuilder.DefineDynamicModule(AssemblyName);
  //      GeneratedTypes = new List<Type>();
  //    }

  //    // Validate the defs, making sure that we don't attempt to double define any.
  //  //  ValidateTypeDefNames(defsFile);

  //    // Now we can build out our types.
  //    // We need to sort them by dependency order, and then validate that any referenced composite
  //    // types actually exist.  This means that our defs files must be loaded in order!
  //    List<TypeDef> sorted = SortTypeDependencies(defsFile.AllDefs);

  //    foreach (var def in sorted)
  //    {
  //      CreateDynamicType(def);
  //    }


  //   // AsmBuilder.Save("built-asm.dll");

  //  }
  //}

  // --------------------------------------------------------------------------------------------------------------------------
  public Type CreateDynamicType(TypeDef def)
  {
    // This needs to be in a lazy property, or wrapped init function!
    lock (AsmBuilderLock)
    {
      if (AsmBuilder == null)
      {
        AsmBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(AssemblyName), AssemblyBuilderAccess.RunAndCollect);

        ModBuilder = AsmBuilder.DefineDynamicModule(AssemblyName);
        GeneratedTypes = new List<Type>();
      }
    }

    lock (TypeResolverLock)
    {
      if (ResolvedTypes.TryGetValue(def.Name, out Type check))
      {
        return check;
      }
    }



    TypeBuilder tb = ModBuilder.DefineType(def.Name, TypeAttributes.Public);

    var withDefaults = new Dictionary<TypeDef.PropertyDef, FieldBuilder>();
    foreach (var p in def.Properties)
    {

      string backingName = "_" + p.Name;

      Type useType = ResolveType(p.Type);

      // We create the backing field, then generate all of the required get/set code.
      FieldBuilder fb = tb.DefineField(backingName, useType, FieldAttributes.Private);

      var pb = tb.DefineProperty(p.Name, PropertyAttributes.HasDefault, useType, null);
      MethodAttributes getSetAttr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;

      // Generate the getter code.
      MethodBuilder getter = tb.DefineMethod($"get_{p.Name}", getSetAttr, useType, Type.EmptyTypes);
      ILGenerator getIL = getter.GetILGenerator();
      getIL.Emit(OpCodes.Ldarg_0);
      getIL.Emit(OpCodes.Ldfld, fb);
      getIL.Emit(OpCodes.Ret);


      // Generate the setter code.
      MethodBuilder setter = tb.DefineMethod($"set_{p.Name}", getSetAttr, null, new Type[] { useType });
      ILGenerator setIL = setter.GetILGenerator();
      setIL.Emit(OpCodes.Ldarg_0);
      setIL.Emit(OpCodes.Ldarg_1);
      setIL.Emit(OpCodes.Stfld, fb);
      setIL.Emit(OpCodes.Ret);


      // Apply the getter / setter to the property:
      pb.SetGetMethod(getter);
      pb.SetSetMethod(setter);


      // Apply any attributes to our type, as needed.
      foreach (var attr in p.Attributes)
      {
        var ctor = attr.AttributeType.GetConstructor(new Type[] { });
        var cab = new CustomAttributeBuilder(ctor, new object[] { });
        pb.SetCustomAttribute(cab);
      }

      // Queue up the defaults for generation.
      if (!string.IsNullOrWhiteSpace(p.Default))
      {
        withDefaults[p] = fb;
      }

    }

    // We now need to create a default constructor that will set all of the default values.
    // Let's set the default values in the default constructor.
    // This means that we need to keep track of the types that we are defining.  This also means that we will have to resolve type hierarches as well.
    ConstructorBuilder ctorb = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, null);
    var ctorIL = ctorb.GetILGenerator();
    foreach (var item in withDefaults)
    {
      TypeDef.PropertyDef prop = item.Key;
      FieldBuilder fb = item.Value;

      // Set the backing field for each of the properties.    
      ctorIL.Emit(OpCodes.Ldarg_0);

      // The next instruction depends on the type.
      // We must resolve the correct opcode + value.
      Type t = ResolveType(prop.Type);
      if (ReflectionTools.IsSimpleType(t))
      {
        switch (prop.Type)
        {
          case "int":
            if (!int.TryParse(prop.Default, out int intVal))
            {
              throw new DynamicGeneratorException($"Could not convert default value: '{prop.Default}' to type '{prop.Type}'!");
            }
            ctorIL.Emit(OpCodes.Ldc_I4, intVal);
            break;

          case "float":
            if (!float.TryParse(prop.Default, out float floatVal))
            {
              throw new DynamicGeneratorException($"Could not convert default value: '{prop.Default}' to type '{prop.Type}'!");
            }
            ctorIL.Emit(OpCodes.Ldc_R4, floatVal);
            break;

          case "double":
            if (!float.TryParse(prop.Default, out float doubleVal))
            {
              throw new DynamicGeneratorException($"Could not convert default value: '{prop.Default}' to type '{prop.Type}'!");
            }
            ctorIL.Emit(OpCodes.Ldc_R8, doubleVal);
            break;

          case "string":
            ctorIL.Emit(OpCodes.Ldstr, prop.Default);
            break;

          default:
            throw new NotSupportedException($"There is no default value support for the type '{prop.Type}'!");
        }
      }
      else
      {
        if (item.Key.Default != "new")
        {
          throw new DynamicGeneratorException($"'new' is the only legal default value for non-simple types!");
        }
        throw new NotSupportedException("no support for default new instances!");
      }

      // Finally, set the value.
      ctorIL.Emit(OpCodes.Stfld, fb);


    }

    // Return from the constructor.
    ctorIL.Emit(OpCodes.Ret);

    Type res = tb.CreateType();

    // Add the generated type!
    string finalName = res.FullName;
    lock (TypeResolverLock)
    {
      ResolvedTypes[finalName] = res;
    }
    GeneratedTypes.Add(res);

    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private Type ResolveType(string typeName)
  {
    lock (TypeResolverLock)
    {
      if (!ResolvedTypes.TryGetValue(typeName, out Type res))
      {
        res = ReflectionTools.ResolveType(typeName);
        if (res == null)
        {
          throw new DynamicGeneratorException($"The type named: {typeName} could not be resolved!");
        }
      }
      return res;
    }
  }

  //// --------------------------------------------------------------------------------------------------------------------------
  ///// <summary>
  ///// Make sure that all referenced types actually exist.
  ///// </summary>
  //private void ValidateDependencies(List<TypeDef> sorted)
  //{
  //    throw new NotImplementedException();
  //}

  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Make sure that we create our types in specific order so we generate dependent types correctly.
  /// </summary>
  private List<TypeDef> SortTypeDependencies(List<TypeDef> allDefs)
  {
    Debug.WriteLine("We are not sorting dependencies at this time!  To be implemented!");
    var res = new List<TypeDef>(allDefs);
    return res;
  }

  //// --------------------------------------------------------------------------------------------------------------------------
  ///// <summary>
  ///// Makes sure that any requested def doesn't already exist.
  ///// </summary>
  ///// <remarks>
  ///// We only check the types defined in DynamicTypeManager instance.  Checking all types in the whole type system might make sense, but seems like overkill at this point in time.
  ///// Other checks could be put in place at this time.
  ///// </remarks>
  //private void ValidateTypeDefNames(TypeDefsFile defsFile)
  //{
  //  foreach (var def in defsFile.AllDefs)
  //  {
  //    // Name check!
  //    var definedType = (from x in GeneratedTypes where x.FullName == def.Name select x).SingleOrDefault();
  //    if (definedType != null)
  //    {
  //      throw new DynamicGeneratorException($"A type named: {def.Name} is already defined!");
  //    }
  //  }
  //}



  // --------------------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Return a list of all of the types that are currently loaded.
  /// </summary>
  /// <returns></returns>
  public ReadOnlyCollection<Type> GetGeneratedTypes()
  {
    lock (AsmBuilderLock)
    {
      var res = new ReadOnlyCollection<Type>(GeneratedTypes);
      return res;
    }
  }




  // --------------------------------------------------------------------------------------------------------------------------
  public object GetTypeInstance(string typeName)
  {
    throw new NotSupportedException();
  }

}

// ============================================================================================================================
// FIX:  Use the typedefs classes from dType, when it becomes available. 
public class TypeDef
{

  public class PropertyDef
  {
    public string Name { get; set; }
    public string Type { get; set; }
    public string Default { get; set; }

    public List<AttributeDef> Attributes { get; set; } = new List<AttributeDef>();
  }

  public class AttributeDef
  {
    public AttributeDef(Type attrType)
    {
      AttributeType = attrType;
    }
    public Type AttributeType { get; private set; }
  }

  public string Name { get; set; }
  public List<PropertyDef> Properties = new List<PropertyDef>();
}




// ============================================================================================================================
public class DynamicGeneratorException : Exception
{
  public DynamicGeneratorException(string message) : base(message)
  { }
}
