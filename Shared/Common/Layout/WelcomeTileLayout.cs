namespace RimShared.Common
{
    public readonly struct WelcomeTile
    {
        public readonly float X, Y, Width, Height;
        public WelcomeTile(float x, float y, float width, float height)
        { X = x; Y = y; Width = width; Height = height; }
    }

    public sealed class WelcomeTilePlan
    {
        private readonly WelcomeTile[] tiles;
        internal WelcomeTilePlan(WelcomeTile[] tiles) { this.tiles = tiles; }
        public int Count => tiles.Length;
        public WelcomeTile At(int index) => tiles[index];
        public bool TryGetTile(int index, out WelcomeTile tile)
        {
            if (index < 0 || index >= tiles.Length) { tile = default; return false; }
            tile = tiles[index];
            return true;
        }
    }

    public static class WelcomeTileLayout
    {
        public static WelcomeTilePlan Create(float width, float height, int requested)
        {
            if (requested <= 0 || width <= 0 || height <= 35)
                return new WelcomeTilePlan(System.Array.Empty<WelcomeTile>());
            const float gap = 12f;
            float availableWidth = System.Math.Max(1f, width - gap * 2f);
            float availableHeight = System.Math.Max(1f, height - 35f - gap * 2f);
            float minimumWidth = System.Math.Min(360f, availableWidth);
            float minimumHeight = System.Math.Min(280f, availableHeight);
            int maximumColumns = System.Math.Max(1,
                (int)((availableWidth + gap) / (minimumWidth + gap)));
            int maximumRows = System.Math.Max(1,
                (int)((availableHeight + gap) / (minimumHeight + gap)));
            int count = System.Math.Min(requested, maximumColumns * maximumRows);
            int columns = 1, rows = count;
            float bestArea = -1f, tileWidth = 0f, tileHeight = 0f;
            for (int candidate = 1; candidate <= System.Math.Min(count, maximumColumns); candidate++)
            {
                int candidateRows = (count + candidate - 1) / candidate;
                if (candidateRows > maximumRows) continue;
                float w = System.Math.Min(600f, (availableWidth - gap * (candidate - 1)) / candidate);
                float h = System.Math.Min(520f, (availableHeight - gap * (candidateRows - 1)) / candidateRows);
                float area = w * h;
                if (area <= bestArea) continue;
                bestArea = area;
                columns = candidate;
                rows = candidateRows;
                tileWidth = (float)System.Math.Floor(w);
                tileHeight = (float)System.Math.Floor(h);
            }
            float left = (float)System.Math.Floor((width - columns * tileWidth - (columns - 1) * gap) / 2f);
            float top = (float)System.Math.Floor((height - 35f - rows * tileHeight - (rows - 1) * gap) / 2f);
            var tiles = new WelcomeTile[count];
            for (int i = 0; i < count; i++)
                tiles[i] = new WelcomeTile(left + (i % columns) * (tileWidth + gap),
                    top + (i / columns) * (tileHeight + gap), tileWidth, tileHeight);
            return new WelcomeTilePlan(tiles);
        }
    }
}
