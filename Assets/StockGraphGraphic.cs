using UnityEngine;
using UnityEngine.UI;

public sealed class StockGraphGraphic : Graphic
{
    private const int HorizontalGridLines = 3;
    private const int VerticalGridLines = 5;
    private const float GridThickness = 1f;
    private const float AxisThickness = 2f;

    [SerializeField] private float lineThickness = 5f;
    [SerializeField] private Color gridColor = new Color(0.58f, 0.92f, 1f, 0.28f);
    [SerializeField] private Color axisColor = new Color(0.72f, 1f, 0.9f, 0.55f);

    private float[] values;
    private int valueCount;
    private RectTransform imageRoot;
    private Image[] lineImages;
    private Image[] horizontalGridImages;
    private Image[] verticalGridImages;
    private Image bottomAxisImage;
    private Image leftAxisImage;
    private bool layoutDirty;

    public void SetValues(float[] sourceValues, int count)
    {
        if (sourceValues == null || count <= 0)
        {
            values = null;
            valueCount = 0;
            SetVerticesDirty();
            layoutDirty = true;
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
        layoutDirty = true;
        RebuildImageChart();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        layoutDirty = true;
    }

    private void LateUpdate()
    {
        if (layoutDirty)
        {
            RebuildImageChart();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        if (values == null || valueCount < 2)
        {
            return;
        }

        Rect rect = rectTransform.rect;

        if (rect.width <= 1f || rect.height <= 1f)
        {
            return;
        }

        AddMeshGrid(vertexHelper, rect);

        float min = values[0];
        float max = values[0];

        for (int i = 1; i < valueCount; i++)
        {
            min = Mathf.Min(min, values[i]);
            max = Mathf.Max(max, values[i]);
        }

        float rawRange = max - min;
        bool isFlat = rawRange < 0.001f;
        float range = Mathf.Max(0.01f, rawRange);
        Vector2 previous = GetPoint(rect, values[0], min, range, 0);

        for (int i = 1; i < valueCount; i++)
        {
            Vector2 current = isFlat
                ? GetFlatPoint(rect, i)
                : GetPoint(rect, values[i], min, range, i);

            if (i == 1 && isFlat)
            {
                previous = GetFlatPoint(rect, 0);
            }

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

    private Vector2 GetFlatPoint(Rect rect, int index)
    {
        float x = rect.xMin + rect.width * index / Mathf.Max(1, valueCount - 1);
        return new Vector2(x, rect.center.y);
    }

    private void RebuildImageChart()
    {
        layoutDirty = false;

        if (values == null || valueCount < 2)
        {
            if (imageRoot != null)
            {
                imageRoot.gameObject.SetActive(false);
            }

            return;
        }

        EnsureImageRoot();
        imageRoot.gameObject.SetActive(true);

        Rect rect = imageRoot.rect;

        if (rect.width <= 1f || rect.height <= 1f)
        {
            layoutDirty = true;
            return;
        }

        EnsureGridImages();
        DrawImageGrid(rect);
        DrawImageLine(rect);
    }

    private void EnsureImageRoot()
    {
        if (imageRoot != null)
        {
            return;
        }

        GameObject rootObject = new GameObject("Visible Chart Lines", typeof(RectTransform));
        rootObject.transform.SetParent(transform, false);
        imageRoot = rootObject.GetComponent<RectTransform>();
        imageRoot.anchorMin = Vector2.zero;
        imageRoot.anchorMax = Vector2.one;
        imageRoot.offsetMin = Vector2.zero;
        imageRoot.offsetMax = Vector2.zero;
    }

    private void EnsureGridImages()
    {
        EnsureImageArray(ref horizontalGridImages, HorizontalGridLines, "Horizontal Grid");
        EnsureImageArray(ref verticalGridImages, VerticalGridLines, "Vertical Grid");

        if (bottomAxisImage == null)
        {
            bottomAxisImage = CreateImageLine("Bottom Axis");
        }

        if (leftAxisImage == null)
        {
            leftAxisImage = CreateImageLine("Left Axis");
        }
    }

    private void DrawImageGrid(Rect rect)
    {
        for (int i = 0; i < horizontalGridImages.Length; i++)
        {
            float y = Mathf.Lerp(0f, rect.height, (i + 1f) / (HorizontalGridLines + 1f));
            SetImageLine(horizontalGridImages[i], new Vector2(0f, y), new Vector2(rect.width, y), GridThickness, gridColor);
        }

        for (int i = 0; i < verticalGridImages.Length; i++)
        {
            float x = Mathf.Lerp(0f, rect.width, (i + 1f) / (VerticalGridLines + 1f));
            SetImageLine(verticalGridImages[i], new Vector2(x, 0f), new Vector2(x, rect.height), GridThickness, gridColor);
        }

        SetImageLine(bottomAxisImage, new Vector2(0f, 0f), new Vector2(rect.width, 0f), AxisThickness, axisColor);
        SetImageLine(leftAxisImage, new Vector2(0f, 0f), new Vector2(0f, rect.height), AxisThickness, axisColor);
    }

    private void DrawImageLine(Rect rect)
    {
        EnsureImageArray(ref lineImages, valueCount - 1, "Price Line");

        float min = values[0];
        float max = values[0];

        for (int i = 1; i < valueCount; i++)
        {
            min = Mathf.Min(min, values[i]);
            max = Mathf.Max(max, values[i]);
        }

        float rawRange = max - min;
        bool isFlat = rawRange < 0.001f;
        float range = Mathf.Max(0.01f, rawRange);
        Vector2 previous = isFlat
            ? GetFlatImagePoint(rect, 0)
            : GetImagePoint(rect, values[0], min, range, 0);

        for (int i = 1; i < valueCount; i++)
        {
            Vector2 current = isFlat
                ? GetFlatImagePoint(rect, i)
                : GetImagePoint(rect, values[i], min, range, i);

            SetImageLine(lineImages[i - 1], previous, current, lineThickness, color);
            previous = current;
        }
    }

    private Vector2 GetImagePoint(Rect rect, float value, float min, float range, int index)
    {
        float x = rect.width * index / Mathf.Max(1, valueCount - 1);
        float y = rect.height * Mathf.InverseLerp(min, min + range, value);
        return new Vector2(x, y);
    }

    private Vector2 GetFlatImagePoint(Rect rect, int index)
    {
        float x = rect.width * index / Mathf.Max(1, valueCount - 1);
        return new Vector2(x, rect.height * 0.5f);
    }

    private void EnsureImageArray(ref Image[] images, int count, string objectName)
    {
        if (images == null)
        {
            images = new Image[count];
        }

        if (images.Length < count)
        {
            Image[] expandedImages = new Image[count];

            for (int i = 0; i < images.Length; i++)
            {
                expandedImages[i] = images[i];
            }

            images = expandedImages;
        }

        for (int i = 0; i < images.Length; i++)
        {
            if (i < count)
            {
                if (images[i] == null)
                {
                    images[i] = CreateImageLine($"{objectName} {i + 1}");
                }

                images[i].gameObject.SetActive(true);
            }
            else if (images[i] != null)
            {
                images[i].gameObject.SetActive(false);
            }
        }
    }

    private Image CreateImageLine(string objectName)
    {
        GameObject lineObject = new GameObject(objectName, typeof(RectTransform));
        lineObject.transform.SetParent(imageRoot, false);

        Image image = lineObject.AddComponent<Image>();
        image.raycastTarget = false;

        RectTransform lineRect = image.rectTransform;
        lineRect.anchorMin = Vector2.zero;
        lineRect.anchorMax = Vector2.zero;
        lineRect.pivot = new Vector2(0.5f, 0.5f);

        return image;
    }

    private static void SetImageLine(Image image, Vector2 start, Vector2 end, float thickness, Color lineColor)
    {
        Vector2 delta = end - start;
        RectTransform lineRect = image.rectTransform;
        lineRect.anchoredPosition = (start + end) * 0.5f;
        lineRect.sizeDelta = new Vector2(Mathf.Max(1f, delta.magnitude), thickness);
        lineRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        image.color = lineColor;
    }

    private void AddMeshGrid(VertexHelper vertexHelper, Rect rect)
    {
        for (int i = 1; i <= HorizontalGridLines; i++)
        {
            float y = Mathf.Lerp(rect.yMin, rect.yMax, i / (HorizontalGridLines + 1f));
            AddLine(vertexHelper, new Vector2(rect.xMin, y), new Vector2(rect.xMax, y), GridThickness, gridColor);
        }

        for (int i = 1; i <= VerticalGridLines; i++)
        {
            float x = Mathf.Lerp(rect.xMin, rect.xMax, i / (VerticalGridLines + 1f));
            AddLine(vertexHelper, new Vector2(x, rect.yMin), new Vector2(x, rect.yMax), GridThickness, gridColor);
        }

        AddLine(vertexHelper, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), AxisThickness, axisColor);
        AddLine(vertexHelper, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax), AxisThickness, axisColor);
    }

    private void AddLine(VertexHelper vertexHelper, Vector2 start, Vector2 end, float thickness)
    {
        AddLine(vertexHelper, start, end, thickness, color);
    }

    private void AddLine(VertexHelper vertexHelper, Vector2 start, Vector2 end, float thickness, Color lineColor)
    {
        Vector2 direction = (end - start).normalized;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector2.right;
        }

        Vector2 normal = new Vector2(-direction.y, direction.x) * thickness * 0.5f;
        int index = vertexHelper.currentVertCount;
        Color32 vertexColor = lineColor;

        vertexHelper.AddVert(start - normal, vertexColor, Vector2.zero);
        vertexHelper.AddVert(start + normal, vertexColor, Vector2.zero);
        vertexHelper.AddVert(end + normal, vertexColor, Vector2.zero);
        vertexHelper.AddVert(end - normal, vertexColor, Vector2.zero);

        vertexHelper.AddTriangle(index, index + 1, index + 2);
        vertexHelper.AddTriangle(index, index + 2, index + 3);
    }
}
