using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nekuzaky.Vicinity.Editor.Tests
{
    internal sealed class PrefabFactoryTests
    {
        #region Main Methods

        [Test]
        public void ASmallObjectStillLoadsFarEnoughAwayToBeUseful()
        {
            Assert.GreaterOrEqual(VicinityPrefabFactory.LoadDistanceForRadius(0.1f), 25f,
                "a pebble that only loaded at arm's length would pop in the player's face");
        }

        [Test]
        public void AHugeObjectDoesNotAskForTheWholeWorld()
        {
            Assert.LessOrEqual(VicinityPrefabFactory.LoadDistanceForRadius(10000f), 400f,
                "an unbounded distance would keep everything resident and defeat the point");
        }

        [Test]
        public void BiggerObjectsLoadFromFurtherAway()
        {
            float small = VicinityPrefabFactory.LoadDistanceForRadius(2f);
            float large = VicinityPrefabFactory.LoadDistanceForRadius(20f);

            Assert.Greater(large, small, "a cathedral is noticed long before a crate is");
        }

        [Test]
        public void DistancesComeOutAsRoundNumbers()
        {
            for (float radius = 0f; radius < 60f; radius += 0.7f)
            {
                float load = VicinityPrefabFactory.LoadDistanceForRadius(radius);

                Assert.AreEqual(0f, load % 5f, 0.001f, $"{load} does not read as a number someone chose");
            }
        }

        [Test]
        public void ReleasingAlwaysHappensFurtherOutThanLoading()
        {
            for (float radius = 0f; radius < 60f; radius += 0.7f)
            {
                float load = VicinityPrefabFactory.LoadDistanceForRadius(radius);
                float release = VicinityPrefabFactory.ReleaseDistanceFor(load);

                Assert.Greater(release, load,
                    "without a margin the object would load and unload on every step near the boundary");
            }
        }

        [Test]
        public void TheProducedPrefabSitsBesideTheOriginal()
        {
            Assert.AreEqual(
                "Assets/Props/Rock (Vicinity).prefab",
                VicinityPrefabFactory.ResultPathFor("Assets/Props/Rock.prefab"));
        }

        [Test]
        public void NothingIsProducedForSomethingThatIsNotAPrefab()
        {
            Assert.IsFalse(VicinityPrefabFactory.CanConvert(null, out string reason));
            Assert.IsNotEmpty(reason, "a refusal without a reason leaves the user stuck");
        }

        [Test]
        public void AnObjectThatDrawsNothingIsRefused()
        {
            string path = FolderPath + "/Empty.prefab";
            GameObject empty = new GameObject("Empty");

            try
            {
                EnsureFolder();
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(empty, path);

                Assert.IsFalse(VicinityPrefabFactory.CanConvert(saved, out string reason));
                Assert.IsNotEmpty(reason);
            }
            finally
            {
                Object.DestroyImmediate(empty);
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void ConvertingAPrefabProducesOneThatStandsInForIt()
        {
            EnsureFolder();

            string sourcePath = FolderPath + "/Boulder.prefab";
            GameObject authored = GameObject.CreatePrimitive(PrimitiveType.Cube);
            authored.transform.localScale = new Vector3(4f, 4f, 4f);

            PrefabConversion conversion = null;

            try
            {
                GameObject source = PrefabUtility.SaveAsPrefabAsset(authored, sourcePath);
                conversion = VicinityPrefabFactory.Convert(source);

                Assert.IsTrue(conversion.Succeeded, conversion.Problem);

                VicinityObject managed = conversion.Result.GetComponent<VicinityObject>();

                Assert.IsNotNull(managed, "the produced prefab is what Vicinity manages, so it must carry the component");
                Assert.IsFalse(managed.HasMissingModel, "it must know which model to load");
                Assert.Greater(managed.UnloadDistance, managed.LoadDistance, "it must be released further out than it loads");
                Assert.Greater(managed.BoundsRadius, 0f, "it draws nothing itself, so it must remember how big it stands");
                Assert.Greater(conversion.Radius, 1f, "a 4 m cube is not a point");
            }
            finally
            {
                Object.DestroyImmediate(authored);

                if (conversion != null && conversion.ResultPath != null)
                {
                    AssetDatabase.DeleteAsset(conversion.ResultPath);
                }

                AssetDatabase.DeleteAsset(sourcePath);
            }
        }

        [Test]
        public void AModelWithATurnedRootIsNotTippedOver()
        {
            EnsureFolder();

            string sourcePath = FolderPath + "/Turned.prefab";
            GameObject authored = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Quaternion turn = Quaternion.Euler(-90f, 0f, 0f);
            authored.transform.localRotation = turn;

            PrefabConversion conversion = null;

            try
            {
                GameObject source = PrefabUtility.SaveAsPrefabAsset(authored, sourcePath);
                conversion = VicinityPrefabFactory.Convert(source);

                Assert.IsTrue(conversion.Succeeded, conversion.Problem);
                Assert.AreEqual(0f, Quaternion.Angle(turn, conversion.Result.transform.localRotation), 0.01f,
                    "an imported model that carries an axis conversion must keep it, or it lies on its side");
            }
            finally
            {
                Object.DestroyImmediate(authored);

                if (conversion != null && conversion.ResultPath != null)
                {
                    AssetDatabase.DeleteAsset(conversion.ResultPath);
                }

                AssetDatabase.DeleteAsset(sourcePath);
            }
        }

        [Test]
        public void AProducedPrefabIsNotTakenOverAgain()
        {
            EnsureFolder();

            string sourcePath = FolderPath + "/Crate.prefab";
            GameObject authored = GameObject.CreatePrimitive(PrimitiveType.Cube);
            PrefabConversion conversion = null;

            try
            {
                GameObject source = PrefabUtility.SaveAsPrefabAsset(authored, sourcePath);
                conversion = VicinityPrefabFactory.Convert(source);

                Assert.IsTrue(conversion.Succeeded, conversion.Problem);
                Assert.IsFalse(VicinityPrefabFactory.CanConvert(conversion.Result, out string reason),
                    "converting the result again would nest stand-ins forever");

                Assert.IsNotEmpty(reason);
            }
            finally
            {
                Object.DestroyImmediate(authored);

                if (conversion != null && conversion.ResultPath != null)
                {
                    AssetDatabase.DeleteAsset(conversion.ResultPath);
                }

                AssetDatabase.DeleteAsset(sourcePath);
            }
        }

        #endregion

        #region Privates

        private const string FolderName = "VicinityFactoryTests";
        private const string FolderPath = "Assets/" + FolderName;

        [TearDown]
        public void RemoveTestFolder()
        {
            if (AssetDatabase.IsValidFolder(FolderPath))
            {
                AssetDatabase.DeleteAsset(FolderPath);
            }
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(FolderPath))
            {
                AssetDatabase.CreateFolder("Assets", FolderName);
            }
        }

        #endregion
    }
}
