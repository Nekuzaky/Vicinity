using NUnit.Framework;
using UnityEngine;

namespace Nekuzaky.Vicinity.Editor.Tests
{
    internal sealed class TargetCameraTests
    {
        #region Main Methods

        [Test]
        public void ACameraParentedUnderTheViewpointIsFound()
        {
            // The usual rig: the target sits on the player root, the camera hangs below it.
            GameObject player = new GameObject("Player");
            _created = player;

            VicinityTarget target = player.AddComponent<VicinityTarget>();

            GameObject head = new GameObject("Head");
            head.transform.SetParent(player.transform, false);
            Camera camera = head.AddComponent<Camera>();

            Assert.AreEqual(camera, target.ViewCamera,
                "a camera below the viewpoint is what the documented behaviour promises");
        }

        [Test]
        public void ACameraOnTheViewpointItselfIsStillFound()
        {
            GameObject player = new GameObject("Player");
            _created = player;

            Camera camera = player.AddComponent<Camera>();
            VicinityTarget target = player.AddComponent<VicinityTarget>();

            Assert.AreEqual(camera, target.ViewCamera);
        }

        [Test]
        public void AViewpointWithNoCameraReportsNoneRatherThanSearchingForever()
        {
            GameObject player = new GameObject("Player");
            _created = player;

            VicinityTarget target = player.AddComponent<VicinityTarget>();

            Assert.IsNull(target.ViewCamera);

            // Reading it again must give the same answer from the remembered miss, which is the whole
            // point: this is read every frame, and a search that finds nothing is the expensive one.
            Assert.IsNull(target.ViewCamera);
        }

        #endregion

        #region Privates

        private GameObject _created;

        [TearDown]
        public void RemoveWhatTheTestBuilt()
        {
            if (_created != null)
            {
                Object.DestroyImmediate(_created);
                _created = null;
            }
        }

        #endregion
    }
}
