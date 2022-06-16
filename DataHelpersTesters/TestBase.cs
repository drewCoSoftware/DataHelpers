
// using System.IO;
// using drewCo.Tools;
// using DataHelpers.Membership;

// // ==========================================================================
// public class TestBase
// {
//   // --------------------------------------------------------------------------------------------------------------------------
//   protected void CleanupTestUser(string username)
//   {
//     IMemberAccess dal = GetMemberAccess();
//     dal.RemoveMember(username);
//   }

//   // --------------------------------------------------------------------------------------------------------------------------
//   protected IMemberAccess GetMemberAccess()
//   {
//     string testDir = Path.Combine(FileTools.GetAppDir(), "TestData");
//     FileTools.CreateDirectory(testDir);

//     // NOTE: We have nothing in here for username/password, and we really should for security purposes.
//     var res = new SqliteMemberAccess(testDir, "MemberMan"); //   FileSystemMemberAccess();
//     if (!File.Exists(res.DBFilePath))
//     {
//       res.SetupDatabase();
//     }

//     return res;
//   }

//   // --------------------------------------------------------------------------------------------------------------------------
//   protected SimEmailService GetEmailService()
//   {
//     return new SimEmailService();
//   }


// }
