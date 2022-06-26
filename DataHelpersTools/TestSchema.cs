using DataHelpers.Data;

// ==========================================================================
public class TestSchema
{
  public List<TestType> TestTable { get; set; } = new List<TestType>();
}

// ==========================================================================
public class TestType : IHasPrimary
{
    public int ID { get; set; }
    public string Name { get; set; }
}
