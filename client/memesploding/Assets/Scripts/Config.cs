using API;
using UnityEngine;

public static class Config
{
    private static ApiConfig _api;

    public static ApiConfig Api
    {
        get
        {
            if (_api == null)
                Load();
            return _api;
        }
    }

    private static void Load()
    {
        TextAsset file = Resources.Load<TextAsset>("config/api_config");

        if (file == null)
        {
            Debug.LogError("API config not found");
            return;
        }

        _api = JsonUtility.FromJson<ApiConfig>(file.text);
    }
}