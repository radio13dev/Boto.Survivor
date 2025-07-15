using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;

[RequireComponent(typeof(CanvasRenderer))]
public class Graph : MaskableGraphic
{
    public class DataSet
    {
        public Point[] points;

        public DataSet(params Point[] points) => this.points = points;
        public DataSet(params (float2, Color)[] points)
        {
            this.points = Array.ConvertAll(points.ToArray(), v => (Point)v);
        }
        public DataSet(List<float2> float2s)
        {
            points = Array.ConvertAll(float2s.ToArray(), v => (Point)v);
        }
        private DataSet(LinkedList<float2> float2s)
        {
            points = new Point[float2s.Count];
            var it = float2s.First;
            int i = 0;
            while (it != null)
            {
                points[i++] = new Point(it.Value);
                it = it.Next;
            }
        }

        public static implicit operator DataSet(LinkedList<float2> points) => new DataSet(points);
        public static implicit operator DataSet(List<float2> points) => new DataSet(points);
        public static implicit operator DataSet((float2, Color)[] points) => new DataSet(points);
    }
    public readonly struct Point
    {
        public readonly float2 point;
        public readonly Color? color;
        
        public float x => point.x;
        public float y => point.y;

        public Point(float2 p, Color? c = null)
        {
            point = p;
            this.color = c;
        }
        
        public static implicit operator Point((float2 point, Color color) point) => new Point(point.point, point.color);
        public static implicit operator Point(float2 point) => new Point(point);
    }

    const int CIRCLE_POINTS = 10;

    public Color graphColor;
    public float lineHeight;

    Coroutine _generatorCo = null;

    protected int _vertexIndex = 0;
    protected List<UIVertex> _vertexStream = new List<UIVertex>(1000);
    protected List<int> _triangleStream = new List<int>(1000);
    protected UIVertex _vert;
    public float2? LastPosition = null;
    public int? LastVertex = null;
    public int PointIndex = 0;
    protected float _genWidth { get; private set; }
    protected float _genHeight { get; private set; }
    protected float _genXMin { get; private set; }
    protected float _genYMin { get; private set; }
    protected float _genXMax { get; private set; }
    protected float _genYMax { get; private set; }
    protected float _genXStep { get; private set; }
    protected float _genXZero { get; private set; }
    protected float _genYStep { get; private set; }
    protected float _genYZero { get; private set; }

    public void Show(DataSet line, float xMin, float xMax, float yMin, float yMax, bool _instant = false)
    {
        if (line == null)
        {
            enabled = false;
            return;
        }

        // Load the prediction data into the mesh, that'll handle it's display when it refreshes.
        _Clear();

        _genWidth = rectTransform.rect.width;
        _genXMin = xMin;
        _genXMax = xMax;
        _genXStep = _genWidth / (_genXMax - _genXMin);
        _genXZero = -_genXStep * _genXMin;
        _genHeight = rectTransform.rect.height;
        _genYMin = yMin;
        _genYMax = yMax;
        if (_genYMax == 0)
            _genYMax = _genHeight;//Stop genYStep becoming infinity, which stops the coordinate Y values down the line from becoming NaN
        _genYStep = _genHeight / (_genYMax - _genYMin);
        _genYZero = -_genYStep * _genYMin;

        // Force a refresh!
        if (_generatorCo != null)
        {
            CoroutineHost.Instance.StopCoroutine(_generatorCo);
            _generatorCo = null;
        }

        // Go over all the data and get averages of the info we want to show with the timestamps we care about.
        if (_instant)
        {
            var co = _GenerateCo(line);
            while (co.MoveNext()) { }
        }
        else
            _generatorCo = CoroutineHost.Instance.StartCoroutine(_GenerateCo(line));
        enabled = true;
    }

    public void Hide() => _Clear();

    protected void _Clear()
    {
        _vert.color = graphColor;

        _vertexIndex = 0;
        _vertexStream.Clear();
        _triangleStream.Clear();
        SetVerticesDirty();
    }

    protected float GetXMax(List<float2> line)
    {
        return Mathf.Max(1, line.Count);
    }

    protected virtual float GetYMax(List<float2> line)
    {
        if (line.Count == 0) return 10;
        return line.Max(l => l.y) * 1.1f;
    }

    [EditorButton]
    public void ShowRandomDummyData()
    {
        const int DUMMY_POINTS = 10;
        const float DUMMY_Y_MAX = 5;
        List<float2> line = new List<float2>();
        for (int i = 0; i < DUMMY_POINTS; i++)
        {
            if (i == 0 || Random.value >= 0.5f)
                line.Add(new float2(i, Random.Range(-DUMMY_Y_MAX, DUMMY_Y_MAX)));
            else
                line.Add(new float2(i, line[i-1].y));
        }
        Show(line, 0, DUMMY_POINTS, Mathf.Min(0, line.Min(l => l.y)), DUMMY_Y_MAX, true);
        
    }

    // actually update our mesh
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        // Clear vertex helper to reset vertices, indices etc.
        vh.Clear();
        if (_vertexIndex >= 3)
            vh.AddUIVertexStream(_vertexStream, _triangleStream);
    }

    private IEnumerator _GenerateCo(DataSet line)
    {
        StartVerticesGeneration(line);

        foreach (var pos in line.points)
        {
            // Add a point for this column.
            AddColumnVertices(pos);

            SetVerticesDirty();
            yield return null;
        }

        FinishVerticesGeneration(line);

        _generatorCo = null;
    }

    protected virtual void StartVerticesGeneration(DataSet line)
    {
        LastPosition = null;
        LastVertex = null;
        PointIndex = 0;

    }
    protected virtual void AddColumnVertices(Point pos)
    {
        _AddLinePoint(pos.x, pos.y);
    }

    protected virtual void FinishVerticesGeneration(DataSet line)
    {
    }

    #region Vertex Generation Tools

    const int CapPoints = 5;
    const float CapAngle = Mathf.PI / (CapPoints + 2);//Add 2 because there will be two points already added.

    protected void _AddLinePoint(float xVal, float yVal)
    {
        float y = yVal * _genYStep + _genYZero;

        if (_vertexIndex == 0)
        {
            // Add some initial points leading up to this so we get a line.
            _vert.position = new Vector3(0, y + lineHeight, 0);
            _vertexStream.Add(_vert);
            _vert.position = new Vector3(0, y - lineHeight, 0);
            _vertexStream.Add(_vert);
            LastVertex = 0;
            LastPosition = new float2(0, y);
            _vertexIndex += 2;
            PointIndex++;
        }

        float x = (xVal - _genXMin) * _genXStep;

        float2 newPosition = new float2(x, y);
        if (LastPosition == null)
            LastPosition = newPosition + new float2(-1, 0);

      

        var direction = VectorUtilities.PointAt(LastPosition.Value, newPosition);

        _vert.position = (LastPosition.Value + direction.TurnLeft() * lineHeight).xy0();
         var oldP1 =_vertexStream.AddWithIndex(_vert);
        _vert.position = (LastPosition.Value + direction.TurnRight() * lineHeight).xy0();
        _vertexStream.AddWithIndex(_vert);
        _vertexIndex += 2;
        LastVertex = oldP1;

        _vert.position = (newPosition + direction.TurnLeft() * lineHeight).xy0();
        var p1 = _vertexStream.AddWithIndex(_vert);
        _vert.position = (newPosition + direction.TurnRight() * lineHeight).xy0();
        var p2 = _vertexStream.AddWithIndex(_vert);

        _vertexIndex += 2;

        if (LastVertex.HasValue)
        {
            _triangleStream.Add(LastVertex.Value);
            _triangleStream.Add(p1);
            _triangleStream.Add(LastVertex.Value + 1);
            _triangleStream.Add(p1);
            _triangleStream.Add(p2);
            _triangleStream.Add(LastVertex.Value + 1);
        }
        //Add Cap
        var capStart = direction.TurnLeft();
        for (int i = 1; i <= CapPoints; i++)
        {
            var capDir = VectorUtilities.RotateVector2D(capStart, -i * CapAngle);//negative for clockwise
            _vert.position = (newPosition + capDir * lineHeight).xy0();
            _vertexStream.Add(_vert);
            _vertexIndex++;
        }

        //Link the triangles
        int vertexIterator = 0;
        for (; vertexIterator < CapPoints; vertexIterator++)
        {
            _triangleStream.Add(p1);
            _triangleStream.Add(p1 + vertexIterator + 1);
            _triangleStream.Add(p1 + vertexIterator + 2);
        }

        _triangleStream.Add(p1);
        _triangleStream.Add(p1 + 1 + vertexIterator);
        _triangleStream.Add(p2);

        LastPosition = newPosition;
        LastVertex = p1;
        PointIndex++;
    }

    #endregion
}