using DataHelpers.Data;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DataHelpersTesters
{

  // ==============================================================================================================================
  /// <summary>
  /// Test cases to handle some more technical Sqlite stuff...
  /// </summary>
  public class SqliteTesters
  {
    // --------------------------------------------------------------------------------------------------------------------------  
    /// <summary>
    /// It is very important that I learn what the mutli-thread transactions are like....
    /// </summary>
    [Test]
    public async Task CanRunTransactionsOnMultipleThreads()
    {
      var factory = new SqliteDataFactory<ExampleSchema>("./test-data", nameof(CanRunTransactionsOnMultipleThreads));
      factory.SetupDatabase();

      const int MAX_THREADS = 5;

      var tasks = new Task[MAX_THREADS];
      for (int i = 0; i < MAX_THREADS; i++)
      {
        var t = Task.Factory.StartNew(() =>
        {
          factory.Transaction(dal =>
          {
            // Simluate long running action...
            Thread.Sleep(100);
            int added = dal.RunExecute($"INSERT INTO {nameof(ExampleSchema.Onesies)} (Name) VALUES (\"xxx\")");
          });
        });
        tasks[i] = t;
      }

      Task.WaitAll(tasks);
    }

  }


}
