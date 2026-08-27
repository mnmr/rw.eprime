namespace EPrimeReadouts.Core
{
    public static class GlyphQuadPlan
    {
        public static int UsableVertexCount(int generatedVertexCount) =>
            generatedVertexCount - generatedVertexCount % 4;
    }
}
