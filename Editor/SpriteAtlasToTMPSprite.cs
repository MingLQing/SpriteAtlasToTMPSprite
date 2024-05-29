using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
using TMPro;
using System.Reflection;
using UnityEditor.Sprites;
using System.Linq;

namespace MingLQing.SpriteAtlasToTMPSprite
{
    public class SpriteAtlasToTMPSpriteTools
    {
        [MenuItem("Assets/SpriteAtlas To TMP_Sprite", true)]
        public static bool SpriteAtlasToTMPSpriteCondition()
        {
            foreach (Object obj in Selection.objects)
            {
                if (obj is SpriteAtlas)
                {
                    return true;
                }
            }
            return false;
        }

        [MenuItem("Assets/SpriteAtlas To TMP_Sprite")]
        public static void SpriteAtlasToTMPSpriteAction()
        {
            foreach (Object obj in Selection.objects)
            {
                if (obj is SpriteAtlas atlas)
                {
                    SpriteAtlasToTMPSprite(atlas);
                }
            }
        }

        public static string ActivePlatforName => GetPlatforName(EditorUserBuildSettings.activeBuildTarget);

        public static string GetPlatforName(BuildTarget buildTarget)
        {
            string platforName;
            switch (buildTarget)
            {
                case BuildTarget.Android:
                    platforName = "Android";
                    break;
                case BuildTarget.iOS:
                    platforName = "iPhone";
                    break;
                case BuildTarget.WebGL:
                    platforName = "WebGL";
                    break;
                case BuildTarget.NoTarget:
                    platforName = "DefaultTexturePlatform";
                    break;
                default:
                    platforName = "Standalone";
                    break;
            }
            return platforName;
        }

        public static void SpriteAtlasToTMPSprite(SpriteAtlas atlas)
        {
            if (atlas == null || atlas.spriteCount <= 0)
            {
                return;
            }

            // temporary settings
            TextureImporterPlatformSettings platformSettings = atlas.GetPlatformSettings(ActivePlatforName);
            bool backupOverridden = platformSettings.overridden;
            platformSettings.overridden = false;
            atlas.SetPlatformSettings(platformSettings);

            TextureImporterPlatformSettings defaultPlatformSettings = atlas.GetPlatformSettings(GetPlatforName(BuildTarget.NoTarget));
            TextureImporterFormat backupFormat = defaultPlatformSettings.format;
            defaultPlatformSettings.format = TextureImporterFormat.RGBA32;
            atlas.SetPlatformSettings(defaultPlatformSettings);

            SpriteAtlasTextureSettings textureSetting = atlas.GetTextureSettings();
            SpriteAtlasTextureSettings backupTextureSetting = textureSetting;
            textureSetting.readable = true;
            atlas.SetTextureSettings(textureSetting);

            SpriteAtlasPackingSettings packingSettings = atlas.GetPackingSettings();
            SpriteAtlasPackingSettings backupPackingSettings = packingSettings;
            packingSettings.enableRotation = false;
            packingSettings.enableTightPacking = false;
            atlas.SetPackingSettings(packingSettings);

            SpriteAtlasUtility.PackAtlases(new SpriteAtlas[] { atlas }, EditorUserBuildSettings.activeBuildTarget, false);

            // export png
            ExportSpriteAtlasTexture(atlas);
            ExportSpriteAsset(atlas);

            // reset settings
            platformSettings.overridden = backupOverridden;
            atlas.SetPlatformSettings(platformSettings);

            defaultPlatformSettings.format = backupFormat;
            atlas.SetPlatformSettings(defaultPlatformSettings);

            atlas.SetTextureSettings(backupTextureSetting);
            atlas.SetPackingSettings(backupPackingSettings);

            SpriteAtlasUtility.PackAtlases(new SpriteAtlas[] { atlas }, EditorUserBuildSettings.activeBuildTarget, false);
        }

        private static string GetTexturePath(SpriteAtlas atlas)
        {
            string atlasPath = AssetDatabase.GetAssetPath(atlas);
            return atlasPath.Replace(".spriteatlas", ".png");
        }

        private static string GetAssetPath(SpriteAtlas atlas)
        {
            string atlasPath = AssetDatabase.GetAssetPath(atlas);
            return atlasPath.Replace(".spriteatlas", ".asset");
        }

        private static void ExportSpriteAtlasTexture(SpriteAtlas atlas)
        {
            Sprite[] sprites = new Sprite[1];
            atlas.GetSprites(sprites);
            Texture2D texture = SpriteUtility.GetSpriteTexture(sprites[0], true);

            byte[] pngBytes = texture.EncodeToPNG();
            string path = GetTexturePath(atlas);
            File.WriteAllBytes(path, pngBytes);

            AssetDatabase.Refresh();

            TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(path);
            textureImporter.textureType = TextureImporterType.Sprite;
            textureImporter.sRGBTexture = true;
            textureImporter.alphaSource = TextureImporterAlphaSource.FromInput;
            textureImporter.alphaIsTransparency = true;
            textureImporter.SaveAndReimport();
        }

        private static SpriteDataObject GetSpriteDataObject(SpriteAtlas atlas)
        {
            Sprite[] sprites = new Sprite[atlas.spriteCount];
            atlas.GetSprites(sprites);
            Texture2D texture = SpriteUtility.GetSpriteTexture(sprites[0], true);

            Meta meta = new Meta()
            {
                app = "https://github.com/MingLQing/SpriteAtlasToTMPSprite.git",
                version = "1.0.0",
                image = $"{atlas.name}.png",
                format = "RGBA8888",
                size = new SpriteSize() { w = texture.width, h = texture.height },
                scale = 1,
            };

            List<Frame> frames = new List<Frame>(sprites.Length);
            for (int i = 0; i < sprites.Length; i++)
            {
                Sprite sprite = sprites[i];

                List<Vector2> uvs = SpriteUtility.GetSpriteUVs(sprite, true).ToList();
                uvs.Sort((a, b) => a.x.CompareTo(b.x));
                float minX = uvs[0].x;
                uvs.Sort((a, b) => a.y.CompareTo(b.y));
                float minY = uvs[0].y;

                float w = Mathf.RoundToInt(sprite.textureRect.width);
                float h = Mathf.RoundToInt(sprite.textureRect.height);

                float x = minX * texture.width;
                float y = texture.height - minY * texture.height - h;

                Frame frame = new Frame()
                {
                    filename = sprite.name.Replace("(Clone)", ".png"),
                    frame = new SpriteFrame() { x = x, y = y, w = w, h = h },
                    rotated = false,
                    trimmed = false,
                    spriteSourceSize = new SpriteFrame() { x = 0, y = 0, w = w, h = h },
                    sourceSize = new SpriteSize() { w = w, h = h },
                    pivot = new Vector2(0f, 1f),
                };

                frames.Add(frame);
            }

            return new SpriteDataObject() { frames = frames, meta = meta };
        }

        private static void ExportSpriteAsset(SpriteAtlas atlas)
        {
            SpriteDataObject spriteDataObject = GetSpriteDataObject(atlas);
            //Debug.Log(JsonUtility.ToJson(spriteDataObject, true));
            Texture2D spriteAtlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GetTexturePath(atlas));

            string path = GetAssetPath(atlas);
            TMP_SpriteAsset spriteAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(path);

            if (spriteAsset == null)
            {
                spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                AssetDatabase.CreateAsset(spriteAsset, path);
            }

            spriteAsset.spriteSheet = spriteAtlasTexture;

            List<TMP_SpriteGlyph> spriteGlyphTable = new List<TMP_SpriteGlyph>();
            List<TMP_SpriteCharacter> spriteCharacterTable = new List<TMP_SpriteCharacter>();

            MethodInfo populateSpriteTablesMethod = typeof(TMP_SpriteAssetImporter).GetMethod("PopulateSpriteTables", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo addDefaultMaterialMethod = typeof(TMP_SpriteAssetImporter).GetMethod("AddDefaultMaterial", BindingFlags.Static | BindingFlags.NonPublic);

            var spriteCharacterTableProperty = typeof(TMP_SpriteAsset).GetProperty("spriteCharacterTable", BindingFlags.Instance | BindingFlags.Public);
            var spriteGlyphTableProperty = typeof(TMP_SpriteAsset).GetProperty("spriteGlyphTable", BindingFlags.Instance | BindingFlags.Public);
            var versionProperty = typeof(TMP_SpriteAsset).GetProperty("version", BindingFlags.Instance | BindingFlags.Public);

            populateSpriteTablesMethod.Invoke(null, new object[] { spriteDataObject, spriteCharacterTable, spriteGlyphTable });

            spriteCharacterTableProperty.SetValue(spriteAsset, spriteCharacterTable);
            spriteGlyphTableProperty.SetValue(spriteAsset, spriteGlyphTable);
            versionProperty.SetValue(spriteAsset, "1.1.0");

            addDefaultMaterialMethod.Invoke(null, new object[] { spriteAsset });

            EditorUtility.SetDirty(spriteAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
