using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Networking;

public class test : MonoBehaviour
{
    [SerializeField] private string addressKey = "Capsule";
    [SerializeField] private KeyCode loadKey = KeyCode.F;

    private bool hasRegisteredWebRequestHook;

    private void Awake()
    {
        RegisterWebRequestHook();
    }

    private void Update()
    {
        if (Input.GetKeyDown(loadKey))
        {
            LoadRemoteAsset();
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            Addressables.ClearDependencyCacheAsync("Capsule");
            print("清除成功");
        }
    }


private async void LoadRemoteAsset()
{
    // 如果这里返回大于 0，说明这次加载前还需要真正下载远程资源。
    AsyncOperationHandle<long> sizeHandle = Addressables.GetDownloadSizeAsync(addressKey);
    await sizeHandle.Task;

    if (sizeHandle.Status == AsyncOperationStatus.Succeeded)
    {
        Debug.Log($"[Addressables] Download size for '{addressKey}': {sizeHandle.Result} bytes");
    }
    else
    {
        Debug.LogError($"[Addressables] Failed to query download size for '{addressKey}'.");
    }

    Addressables.Release(sizeHandle);

    // 通过 Addressables 的地址加载资源；如果它属于远程包，这一步可能会触发网络下载。
    AsyncOperationHandle<GameObject> loadHandle = Addressables.LoadAssetAsync<GameObject>(addressKey);
    await loadHandle.Task;

    if (loadHandle.Status != AsyncOperationStatus.Succeeded)
    {
        Debug.LogError($"[Addressables] Failed to load '{addressKey}'.");
        Addressables.Release(loadHandle);
        return;
    }

    Instantiate(loadHandle.Result);
    Debug.Log($"[Addressables] Successfully loaded and instantiated '{addressKey}'.");
    Addressables.Release(loadHandle);
}

private void RegisterWebRequestHook()
{
    if (hasRegisteredWebRequestHook)
    {
        return;
    }

    // 打印最终请求的 URL，方便确认资源是不是从 GitHub Pages 拉下来的。
    Addressables.WebRequestOverride = LogWebRequestUrl;
    hasRegisteredWebRequestHook = true;
}

private static void LogWebRequestUrl(UnityWebRequest request)
{
    Debug.Log($"[Addressables] Request URL: {request.url}");
}
}
