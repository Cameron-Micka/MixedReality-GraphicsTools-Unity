using UnityEngine;
using System.IO;

public class BundleObjectLoader : MonoBehaviour
{
    public string assetName = "Sphere";
    public string bundleName = "testbundle";

    void Start()
    {
        AssetBundle localAssetBundle = AssetBundle.LoadFromFile(Path.Combine(Application.streamingAssetsPath, bundleName));

        if (localAssetBundle == null)
        {
            Debug.LogError("Failed!");
            return;
        }

        GameObject asset = localAssetBundle.LoadAsset<GameObject>(assetName);
        Instantiate(asset);

        localAssetBundle.Unload(false);
    }
}
