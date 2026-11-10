using DataHelpers;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace DataHelpersTesters;

public class UniqueAssociationTesters : TestBase
{
  [TearDown]
  public void ReleaseDatabaseConnections()
  {
    SqliteConnection.ClearAllPools();
  }

  /// <summary>
  /// Verifies that [Unique] on Single associations behaves like [Unique] on scalar members:
  /// standalone values must be unique, while Group requires only the association/scalar pair
  /// to be unique. Covers rejection of duplicates for both forms and acceptance of distinct pairs.
  /// </summary>
  [TestCase(false)]
  [TestCase(true)]
  public void SingleAssociationsEnforceUniqueIndexes(bool duplicateGroup)
  {
    string databaseName = nameof(SingleAssociationsEnforceUniqueIndexes);
    var factory = CreateTestDataBaseFor<UniqueAssociationSchema>(databaseName);
    var targets = new UniqueAssociationTarget[4];

    for (int i = 0; i < targets.Length; i++)
    {
      var target = new UniqueAssociationTarget { Number = i + 1 };
      factory.Add(target);
      targets[i] = target;
    }

    var owners = new[]
    {
      new UniqueAssociationOwner { Number = 1, Target = targets[0], GroupedTarget = targets[0] },
      new UniqueAssociationOwner { Number = 2, Target = targets[1], GroupedTarget = targets[0] },
      new UniqueAssociationOwner { Number = 1, Target = targets[2], GroupedTarget = targets[1] }
    };

    foreach (var owner in owners)
    {
      factory.Add(owner);
    }

    var duplicate = new UniqueAssociationOwner
    {
      Number = duplicateGroup ? 1 : 3,
      Target = duplicateGroup ? targets[3] : targets[0],
      GroupedTarget = targets[0]
    };
    var exception = Assert.Throws<SqliteException>(() => factory.Add(duplicate));
    Assert.That(exception!.SqliteExtendedErrorCode == 2067, "Expected a unique constraint violation.");

    int count = factory.GetCount<UniqueAssociationOwner>();
    Assert.That(count == 3);

    var saved = factory.GetById<UniqueAssociationOwner>(owners[2].ID);
    Assert.That(saved != null);
    Assert.That(saved!.Target.ID == targets[2].ID);
    Assert.That(saved.GroupedTarget.ID == targets[1].ID);
    Assert.That(saved.Number == 1);
  }

  /// <summary>
  /// Verifies that [Unique] on Many associations behaves like [Unique] on scalar members:
  /// standalone values must be unique, while Group requires only the association/scalar pair
  /// to be unique. Covers rejection of duplicates for both forms and acceptance of distinct pairs.
  /// </summary>
  [TestCase(false)]
  [TestCase(true)]
  public void ManyAssociationsEnforceUniqueIndexes(bool duplicateGroup)
  {
    string databaseName = nameof(ManyAssociationsEnforceUniqueIndexes);
    var factory = CreateTestDataBaseFor<UniqueAssociationSchema>(databaseName);
    var owners = new UniqueAssociationOwner[4];

    for (int i = 0; i < owners.Length; i++)
    {
      var target = new UniqueAssociationTarget { Number = i + 1 };
      factory.Add(target);

      var owner = new UniqueAssociationOwner { Number = i + 1, Target = target, GroupedTarget = target };
      factory.Add(owner);
      owners[i] = owner;
    }

    var children = new[]
    {
      new UniqueAssociationChild { Number = 1, Owner = owners[0], GroupedOwner = owners[0] },
      new UniqueAssociationChild { Number = 2, Owner = owners[1], GroupedOwner = owners[0] },
      new UniqueAssociationChild { Number = 1, Owner = owners[2], GroupedOwner = owners[1] }
    };

    foreach (var child in children)
    {
      factory.Add(child);
    }

    var duplicate = new UniqueAssociationChild
    {
      Number = duplicateGroup ? 1 : 3,
      Owner = duplicateGroup ? owners[3] : owners[0],
      GroupedOwner = owners[0]
    };
    var exception = Assert.Throws<SqliteException>(() => factory.Add(duplicate));
    Assert.That(exception!.SqliteExtendedErrorCode == 2067, "Expected a unique constraint violation.");

    int count = factory.GetCount<UniqueAssociationChild>();
    Assert.That(count == 3);

    var saved = factory.GetById<UniqueAssociationChild>(children[2].ID);
    Assert.That(saved != null);
    Assert.That(saved!.Owner.ID == owners[2].ID);
    Assert.That(saved.GroupedOwner.ID == owners[1].ID);
    Assert.That(saved.Number == 1);
  }
}
