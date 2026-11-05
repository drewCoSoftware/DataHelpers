using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataHelpers;
using DataHelpers.Data;
using NUnit.Framework;

namespace DataHelpersTesters;

public class CallbackRegistrationTesters : TestBase
{
  [Test]
  public void RegistrationRejectsNullCallbacks()
  {
    var error = Assert.Throws<ArgumentNullException>(() => DBHandler<ConcurrentSchema>.RegisterCallback<CallbackPerson>(null!));

    Assert.That(error!.ParamName, Is.EqualTo("cb"));
  }

  public class CallbackPerson : IHasPrimary
  {
    public int ID { get; set; }
    public string Name { get; set; } = "";
  }

  public class ConcurrentSchema
  {
    public List<CallbackPerson> People { get; set; } = new List<CallbackPerson>();
  }

  public class ReentrantSchema
  {
    public List<CallbackPerson> People { get; set; } = new List<CallbackPerson>();
  }

  [Test]
  public void RegistrationCanRunAlongsideQueriesWithoutLosingCallbacks()
  {
    var factory = CreateTestDataBaseFor<ConcurrentSchema>(nameof(RegistrationCanRunAlongsideQueriesWithoutLosingCallbacks));
    factory.Add(new CallbackPerson { Name = "Test" });

    const int callbackCount = 64;
    var calls = new int[callbackCount];

    Parallel.Invoke(
      () => Parallel.For(0, callbackCount, index =>
      {
        DBHandler<ConcurrentSchema>.RegisterCallback<CallbackPerson>(person =>
        {
          Interlocked.Increment(ref calls[index]);
        });
      }),
      () => Parallel.For(0, callbackCount, index =>
      {
        factory.GetAll<CallbackPerson>();
      }));

    var before = (int[])calls.Clone();
    factory.GetAll<CallbackPerson>();

    for (int index = 0; index < callbackCount; index++)
    {
      Assert.That(calls[index], Is.EqualTo(before[index] + 1));
    }
  }

  [Test]
  public void CallbackCanRegisterAnotherCallbackWithoutChangingCurrentSnapshot()
  {
    var factory = CreateTestDataBaseFor<ReentrantSchema>(nameof(CallbackCanRegisterAnotherCallbackWithoutChangingCurrentSnapshot));
    factory.Add(new CallbackPerson { Name = "Test" });
    int originalCalls = 0;
    int addedCalls = 0;

    DBHandler<ReentrantSchema>.RegisterCallback<CallbackPerson>(person =>
    {
      originalCalls++;

      if (originalCalls == 1)
      {
        DBHandler<ReentrantSchema>.RegisterCallback<CallbackPerson>(other =>
        {
          addedCalls++;
        });
      }
    });

    factory.GetAll<CallbackPerson>();

    Assert.That(originalCalls, Is.EqualTo(1));
    Assert.That(addedCalls, Is.Zero);

    factory.GetAll<CallbackPerson>();

    Assert.That(originalCalls, Is.EqualTo(2));
    Assert.That(addedCalls, Is.EqualTo(1));
  }
}
