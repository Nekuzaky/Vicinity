using NUnit.Framework;
using Nekuzaky.Vicinity.Impostors;
using UnityEngine;

namespace Nekuzaky.Vicinity.Editor.Tests
{
    internal sealed class OctahedralMappingTests
    {
        [Test]
        public void EveryDecodedDirectionIsAUnitVector()
        {
            const int frames = 8;

            for (int row = 0; row < frames; row++)
            {
                for (int column = 0; column < frames; column++)
                {
                    Vector3 direction = OctahedralMapping.DirectionForTile(column, row, frames);
                    Assert.AreEqual(1f, direction.magnitude, 0.0001f, $"tile {column},{row} is not normalised");
                }
            }
        }

        [Test]
        public void ADirectionSurvivesARoundTrip()
        {
            Vector3[] samples =
            {
                Vector3.up,
                Vector3.down,
                Vector3.forward,
                Vector3.back,
                Vector3.left,
                Vector3.right,
                new Vector3(1f, 2f, -3f),
                new Vector3(-0.4f, 0.9f, 0.2f)
            };

            foreach (Vector3 sample in samples)
            {
                Vector2 encoded = OctahedralMapping.Encode(sample);
                Vector3 decoded = OctahedralMapping.Decode(encoded);

                Assert.AreEqual(1f, Vector3.Dot(sample.normalized, decoded), 0.001f,
                    $"{sample} did not survive encode then decode");
            }
        }

        [Test]
        public void TheCentreOfTheSquareLooksStraightAhead()
        {
            Vector3 direction = OctahedralMapping.Decode(new Vector2(0.5f, 0.5f));

            Assert.AreEqual(1f, Vector3.Dot(direction, Vector3.forward), 0.001f);
        }

        [Test]
        public void OppositeCornersLookBackwards()
        {
            Vector3 corner = OctahedralMapping.Decode(Vector2.zero);

            Assert.Less(Vector3.Dot(corner, Vector3.forward), 0f,
                "the corners of the square must cover the hemisphere behind the object");
        }

        [Test]
        public void DirectionsSpreadOverTheWholeSphere()
        {
            const int frames = 8;
            bool sawFront = false;
            bool sawBack = false;
            bool sawUp = false;
            bool sawDown = false;

            for (int row = 0; row < frames; row++)
            {
                for (int column = 0; column < frames; column++)
                {
                    Vector3 direction = OctahedralMapping.DirectionForTile(column, row, frames);

                    sawFront |= direction.z > 0.5f;
                    sawBack |= direction.z < -0.5f;
                    sawUp |= direction.y > 0.5f;
                    sawDown |= direction.y < -0.5f;
                }
            }

            Assert.IsTrue(sawFront && sawBack && sawUp && sawDown,
                "an 8x8 grid must cover front, back, above and below");
        }

        [Test]
        public void NoTwoInteriorTilesCaptureTheSameDirection()
        {
            const int frames = 6;
            System.Collections.Generic.List<Vector3> seen = new System.Collections.Generic.List<Vector3>();

            for (int row = 1; row < frames - 1; row++)
            {
                for (int column = 1; column < frames - 1; column++)
                {
                    Vector3 direction = OctahedralMapping.DirectionForTile(column, row, frames);

                    foreach (Vector3 other in seen)
                    {
                        Assert.Less(Vector3.Dot(direction, other), 0.9999f,
                            "two interior tiles would store the same snapshot, wasting atlas space");
                    }

                    seen.Add(direction);
                }
            }
        }

        [Test]
        public void TheFourCornersAllLookStraightBack()
        {
            Vector3 backwards = Vector3.back;

            Vector2[] corners =
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };

            foreach (Vector2 corner in corners)
            {
                Assert.AreEqual(1f, Vector3.Dot(OctahedralMapping.Decode(corner), backwards), 0.001f,
                    "the square folds onto the sphere, so its four corners are the same point behind the object");
            }
        }

        [Test]
        public void TilesCoverTheAtlasWithoutOverlapping()
        {
            const int frames = 4;
            const int atlasSize = 1024;

            RectInt first = OctahedralMapping.TileRect(0, 0, frames, atlasSize);
            RectInt last = OctahedralMapping.TileRect(frames - 1, frames - 1, frames, atlasSize);

            Assert.AreEqual(256, first.width);
            Assert.AreEqual(0, first.x);
            Assert.AreEqual(768, last.x);
            Assert.AreEqual(atlasSize, last.xMax, "the last tile must end exactly at the atlas edge");
        }

        [Test]
        public void ASingleFrameLooksStraightAhead()
        {
            Assert.AreEqual(Vector3.forward, OctahedralMapping.DirectionForTile(0, 0, 1));
        }
    }
}
