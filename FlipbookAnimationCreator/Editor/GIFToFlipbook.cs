using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using MG.GIF;
using UnityEditor.Animations;

public class GIFToFlipbookConverter : EditorWindow
{
    [MenuItem("Assets/Create Flipbook from GIF")]
    private static void CreateFlipbook()
    {
        string gifPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (Path.GetExtension(gifPath).ToLower() != ".gif")
        {
            Debug.LogError("Selected file is not a GIF.");
            return;
        }

        string gifName = Path.GetFileNameWithoutExtension(gifPath);
        string outputFolder = Path.GetDirectoryName(gifPath) + "/" + gifName + "_Flipbook";

        // Create output folder
        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            AssetDatabase.CreateFolder(Path.GetDirectoryName(gifPath), gifName + "_Flipbook");
        }

        List<Texture2D> frames = new List<Texture2D>();
        List<float> frameDelays = new List<float>();

        // Extract frames from GIF
        byte[] gifData = File.ReadAllBytes(gifPath);
        using (var decoder = new Decoder(gifData))
        {
            var img = decoder.NextImage();
            while (img != null)
            {
                frames.Add(img.CreateTexture());
                frameDelays.Add(img.Delay / 1000.0f);
                img = decoder.NextImage();
            }
        }

        if (frames.Count == 0)
        {
            Debug.LogError("No frames were extracted from the GIF.");
            return;
        }

        // Save individual frames and set texture type to Sprite
        for (int i = 0; i < frames.Count; i++)
        {
            string framePath = $"{outputFolder}/{gifName}_frame_{i:D3}.png";
            File.WriteAllBytes(framePath, frames[i].EncodeToPNG());
            AssetDatabase.ImportAsset(framePath);

            TextureImporter textureImporter = AssetImporter.GetAtPath(framePath) as TextureImporter;
            textureImporter.textureType = TextureImporterType.Sprite;
            textureImporter.SaveAndReimport();
        }

        // Convert Texture2D to Sprite
        List<Sprite> sprites = new List<Sprite>();
        for (int i = 0; i < frames.Count; i++)
        {
            string framePath = $"{outputFolder}/{gifName}_frame_{i:D3}.png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(framePath);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            sprites.Add(sprite);
        }

        // Create Animation Clip
        AnimationClip animClip = new AnimationClip();
        animClip.frameRate = 25; // You can adjust this if needed

        EditorCurveBinding spriteBinding = new EditorCurveBinding();
        spriteBinding.type = typeof(SpriteRenderer);
        spriteBinding.path = "";
        spriteBinding.propertyName = "m_Sprite";

        ObjectReferenceKeyframe[] spriteKeyFrames = new ObjectReferenceKeyframe[sprites.Count];
        float timeSum = 0;

        for (int i = 0; i < sprites.Count; i++)
        {
            spriteKeyFrames[i] = new ObjectReferenceKeyframe();
            spriteKeyFrames[i].time = timeSum;
            spriteKeyFrames[i].value = sprites[i];
            timeSum += frameDelays[i];
        }

        AnimationUtility.SetObjectReferenceCurve(animClip, spriteBinding, spriteKeyFrames);

        string animClipPath = $"{outputFolder}/{gifName}_Anim.anim";
        AssetDatabase.CreateAsset(animClip, animClipPath);

        // Create Animator Controller
        string animControllerPath = $"{outputFolder}/{gifName}_Controller.controller";
        AnimatorController animController = AnimatorController.CreateAnimatorControllerAtPath(animControllerPath);
        AnimatorState state = animController.layers[0].stateMachine.AddState("Play");
        state.motion = animClip;

        // Create GameObject in scene
        GameObject newObject = new GameObject($"{gifName}_Flipbook");
        SpriteRenderer spriteRenderer = newObject.AddComponent<SpriteRenderer>();
        Animator animator = newObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = animController;

        spriteRenderer.sprite = sprites[0];

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Flipbook created successfully!");
    }
}
