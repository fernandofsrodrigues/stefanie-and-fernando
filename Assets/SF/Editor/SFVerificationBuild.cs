using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace StefanieAndFernando.Editor
{
    // Fresh diagnostic builds never reuse a release directory or run Prepare over it.
    public static class SFVerificationBuild
    {
        public static void Windows()
        {
            string name=Environment.GetEnvironmentVariable("SF_VERIFICATION_STAGE");
            if(name!="baseline"&&name!="fixed"&&name!="validated")throw new Exception("Explicit verification stage required");
            string root="../QA/NativeVerification-v0.8.47/"+name;
            string attempt=Environment.GetEnvironmentVariable("SF_VERIFICATION_ATTEMPT");
            if(!string.IsNullOrEmpty(attempt))
            {
                if(!System.Text.RegularExpressions.Regex.IsMatch(attempt,"^[a-zA-Z0-9_-]+$"))throw new Exception("Invalid verification attempt");
                root+="-"+attempt;
            }
            string output=root+"/Windows/StefanieAndFernando.exe";
            if(Directory.Exists(root+"/Windows"))throw new Exception("Preserve existing verification build: "+root);
            Directory.CreateDirectory(root);
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/SF/Scenes/Curitiba.unity"},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            File.WriteAllText(root+"/build-result.txt",result.summary.result+"\nErrors: "+result.summary.totalErrors+"\nWarnings: "+result.summary.totalWarnings);
            if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Verification build failed");
        }
    }
}
