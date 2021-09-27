using System;

namespace BranchReload2
{
    public class GitFileConfigs
    {
        public string UserName { get; set; }
        public string RepoName { get; set; }
        public string BranchId { get; set; }
        
        //public string AccessToken { get; set; }
        public string Key { get; set; }
        
        public const string UNSET_KEY = nameof(UNSET_KEY);

        private string _RepoUrl { get; set; }

        public string GetAccessToken()
        {
            return Environment.GetEnvironmentVariable(EnvironmentWriter.AccessTokenTarget,
                EnvironmentVariableTarget.User);
        }
        
        public string RepoUrl() => _RepoUrl ??= 
            $"https://{Environment.GetEnvironmentVariable(EnvironmentWriter.AccessTokenTarget, EnvironmentVariableTarget.User)}@github.com/{UserName}/{RepoName}.git";

        public bool EmptyValue(string val)
        {
            return string.IsNullOrEmpty(val) || string.IsNullOrWhiteSpace(val);
        }
        
        public bool HasValidEntries()
        {
            if (EmptyValue(UserName))
                return false;
            
            if (EmptyValue(RepoName))
                return false;

            return true;
        }

        /// <summary>
        /// If the key is empty, or unset, that means the user is expected to enter it in.
        /// The key is replaced by a hash when the program detects an access_token.
        /// </summary>
        public bool KeyIsUnset() => EmptyValue(Key) || Key is UNSET_KEY;

        public bool KeyHashIsValid()
        {
            if (Key.Contains(":"))
            {
                string[] parts = Key.Split(":");
                int step1 = GetValueOfString("abcd" + parts[0]);
                Console.WriteLine(step1+":"+parts[1]);
                return (step1+"").Equals(parts[1]);
            }
            return false;
        }

        public int GetValueOfString(string s)
        {
            int i = 0;
            foreach (var c in s) i += (byte) c;
            return i;
        }
        
        public string GetHash()
        {
            int partA = GetValueOfString(Key);
            int partB = GetValueOfString("abcd" + partA);
            return $"{partA}:{partB}";
        }
    }
}