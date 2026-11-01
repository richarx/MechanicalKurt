using System.IO;
using UnityEngine;

public class TextureToPNG : MonoBehaviour
{
    // Drag your texture into this slot in the Unity Inspector
    public Texture2D myTexture;

    void Start()
    {
        if (myTexture != null)
        {
            SaveTextureAsPNG(myTexture, "MyExportedTexture.png");
        }
        else
        {
            Debug.LogError("No texture assigned to export!");
        }
    }

    public void SaveTextureAsPNG(Texture2D texture, string fileName)
    {
        // 1. Encode the texture into a PNG byte array
        byte[] bytes = ImageConversion.EncodeToPNG(texture);

        // 2. Define the save path (this saves it into your project's main folder)
        string savePath = Path.Combine(Application.dataPath, "..", fileName);

        // 3. Write the bytes to the disk
        File.WriteAllBytes(savePath, bytes);

        Debug.Log($"Texture successfully saved to: {Path.GetFullPath(savePath)}");
    }
}