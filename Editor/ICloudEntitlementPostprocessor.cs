using System.Collections;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

public class ICloudEntitlementPostprocessor : ScriptableObject
{

	[SerializeField] private DefaultAsset _entitlementFile;

	[PostProcessBuild(999)]
	public static void OnPostProcessBuild(BuildTarget buildTarget, string builtProjectPath)
	{
		if (buildTarget == BuildTarget.iOS) {

			#if UNITY_IOS

			// Get the XCode project
			string projPath = builtProjectPath + "/Unity-iPhone.xcodeproj/project.pbxproj";
			PBXProject pbxProject = new PBXProject();
			pbxProject.ReadFromString(File.ReadAllText(projPath));
			string targetName = PBXProject.GetUnityTargetName();
			string targetGuid = pbxProject.TargetGuidByName("Unity-iPhone");

			// Add entitlement file
			var dummy = ScriptableObject.CreateInstance<ICloudEntitlementPostprocessor>();
			var entitlementsFile = dummy._entitlementFile;
			ScriptableObject.DestroyImmediate(dummy);

			if (entitlementsFile != null) {
				var entitlementsSrcPath = AssetDatabase.GetAssetPath(entitlementsFile);
				var entitlementsFileName = Path.GetFileName(entitlementsSrcPath);
				var entitlementsDistPath = builtProjectPath + "/" + targetName + "/" + entitlementsFileName;
				FileUtil.CopyFileOrDirectory(entitlementsSrcPath, entitlementsDistPath);
				pbxProject.AddFile(targetName + "/" + entitlementsFileName, entitlementsFileName);
				pbxProject.AddBuildProperty(targetGuid, "CODE_SIGN_ENTITLEMENTS", targetName + "/" + entitlementsFileName);
				pbxProject.WriteToFile(projPath);
			}

			#endif

		}

	}

}