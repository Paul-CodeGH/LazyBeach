using UnityEngine;
using UnityEngine.UI;

public sealed class StockGraphGraphic : Graphic
{
    [SerializeField] private float lineThickness = 3f;

    private float[] values;
    private int valueCount;

    public void SetValues(float[] sourceValues, int count)
    {
        if (sourceValues == null || count <= 0)
        {
            values = null;
            valueCount = 0;
            SetVerticesDirty();
            return;
        }

        valueCount = Mathf.Min(count, sourceValues.Length);

        if (values == null || values.Length != valueCount)
        {
            values = new float[valueCount];
        }

        int sourceStart = Mathf.Max(0, sourceValues.Length - valueCount);

        for (int i = 0; i < valueCount; i++)
        {
            values[i] = sourceValues[sourceStart + i];
        }

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        if (values == null || valueCount < 2)
        {
            return;
        }

        Rect rect = rectTransform.rect;
        float min = values[0];
        float max = values[0];

        for (int i = 1; i < valueCount; i++)
        {
            min = Mathf.Min(min, values[i]);
            max = Mathf.Max(max, values[i]);
        }

        float range = Mathf.Max(0.01f, max - min);
        Vector2 previous = GetPoint(rect, values[0], min, range, 0);

        for (int i = 1; i < valueCount; i++)
        {
            Vector2 current = GetPoint(rect, values[i], min, range, i);
            AddLine(vertexHelper, previous, current, lineThickness);
            previous = current;
        }
    }

    private Vector2 GetPoint(Rect rect, float value, float min, float range, int index)
    {
        float x = rect.xMin + rect.width * index / Mathf.Max(1, valueCount - 1);
        float y = rect.yMin + rect.height * Mathf.InverseLerp(min, min + range, value);
        return new Vector2(x, y);
    }

    private void AddLine(VertexHelper vertexHelper, Vector2 start, Vector2 end, float thickness)
    {
        Vector2 direction = (end - start).normalized;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector2.right;
        }

        Vector2 normal = new Vector2(-direction.y, direction.x) * thickness * 0.5f;
        int index = vertexHelper.currentVertCount;
        Color32 lineColor = color;

        vertexHelper.AddVert(start - normal, lineColor, Vector2.zero);
        vertexHelper.AddVert(start + normal, lineColor, Vector2.zero);
        vertexHelper.AddVert(end + normal, lineColor, Vector2.zero);
        vertexHelper.AddVert(end - normal, lineColor, Vector2.zero);

        vertexHelper.AddTriangle(index, index + 1, index + 2);
        vertexHelper.AddTriangle(index, index + 2, index + 3);
    }
}
