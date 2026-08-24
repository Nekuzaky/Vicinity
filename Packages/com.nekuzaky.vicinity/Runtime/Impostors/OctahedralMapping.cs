using UnityEngine;

namespace Nekuzaky.Vicinity.Impostors
{
    /// <summary>
    /// Turns a square grid of tiles into directions spread evenly around a sphere, and back.
    /// An impostor stores one snapshot per tile; at runtime the shader picks the tile that matches
    /// the direction the camera is looking from.
    /// </summary>
    public static class OctahedralMapping
    {
        #region Main Methods

        /// <summary>Turns a point on the unit square into the direction it stands for.</summary>
        public static Vector3 Decode(Vector2 squarePoint)
        {
            Vector2 folded = squarePoint * 2f - Vector2.one;

            Vector3 direction = new Vector3(
                folded.x,
                folded.y,
                1f - Mathf.Abs(folded.x) - Mathf.Abs(folded.y));

            if (direction.z < 0f)
            {
                float x = (1f - Mathf.Abs(direction.y)) * SignOf(direction.x);
                float y = (1f - Mathf.Abs(direction.x)) * SignOf(direction.y);

                direction.x = x;
                direction.y = y;
            }

            return direction.normalized;
        }

        /// <summary>Turns a direction into the point on the unit square that stores it.</summary>
        public static Vector2 Encode(Vector3 direction)
        {
            Vector3 normalized = direction.normalized;
            float sum = Mathf.Abs(normalized.x) + Mathf.Abs(normalized.y) + Mathf.Abs(normalized.z);

            if (sum < Mathf.Epsilon)
            {
                return new Vector2(0.5f, 0.5f);
            }

            normalized /= sum;

            Vector2 folded = normalized.z >= 0f
                ? new Vector2(normalized.x, normalized.y)
                : new Vector2(
                    (1f - Mathf.Abs(normalized.y)) * SignOf(normalized.x),
                    (1f - Mathf.Abs(normalized.x)) * SignOf(normalized.y));

            return folded * 0.5f + new Vector2(0.5f, 0.5f);
        }

        /// <summary>The direction captured by one tile of a grid that is <paramref name="frames"/> wide.</summary>
        public static Vector3 DirectionForTile(int column, int row, int frames)
        {
            if (frames <= 1)
            {
                return Vector3.forward;
            }

            float divisor = frames - 1;
            Vector2 squarePoint = new Vector2(column / divisor, row / divisor);

            return Decode(squarePoint);
        }

        /// <summary>Where a tile sits inside the atlas, as a rectangle in pixels.</summary>
        public static RectInt TileRect(int column, int row, int frames, int atlasSize)
        {
            int tileSize = atlasSize / Mathf.Max(frames, 1);
            return new RectInt(column * tileSize, row * tileSize, tileSize, tileSize);
        }

        #endregion

        #region Privates

        private static float SignOf(float value) => value >= 0f ? 1f : -1f;

        #endregion
    }
}
