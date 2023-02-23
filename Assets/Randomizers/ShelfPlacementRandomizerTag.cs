using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DefaultNamespace;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Randomizers;
using UnityEngine.Perception.Randomization.Samplers;
using UnityEngine.Perception.Randomization.Utilities;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public class ShelfPlacementRandomizerTag : RandomizerTag
{
}

[Serializable]
[AddRandomizerMenu("Shelf placement Randomizer")]
public class ShelfPlacementRandomizer : Randomizer
{
    public CategoricalParameter<GameObject> prefabs;

    public float minDepth;
    public float maxDepth;
    
    public float minStack;
    public float maxStack;
    
    GameObject m_Container;
    GameObjectOneWayCache m_GameObjectOneWayCache;
    private FloatParameter _indexSelector = new FloatParameter { value = new UniformSampler(0, 1) };

    private Dictionary<GameObject, float> _gameObjectBoundsSizeCacheX;
    private Dictionary<GameObject, float> _gameObjectBoundsSizeCacheY;
    private Dictionary<GameObject, float> _gameObjectBoundsSizeCacheZ;
    private float _maxBoundsSizeX;
    private float _minBoundsSizeX;
    private float _maxBoundsSizeY;
    private float _minBoundsSizeY;
    private float _maxBoundsSizeZ;
    private float _minBoundsSizeZ;

    protected override void OnAwake()
    {
        m_Container = new GameObject("Objects");
        m_Container.transform.parent = scenario.transform;
        m_GameObjectOneWayCache = new GameObjectOneWayCache(
            m_Container.transform,
            prefabs.categories.Select(element => element.Item1).ToArray(),
            this);
        _gameObjectBoundsSizeCacheX = new Dictionary<GameObject, float>();
        _gameObjectBoundsSizeCacheY = new Dictionary<GameObject, float>();
        _gameObjectBoundsSizeCacheZ = new Dictionary<GameObject, float>();

        CalculateProductsLength();
        _maxBoundsSizeX = _gameObjectBoundsSizeCacheX.Values.Max();
        _minBoundsSizeX = _gameObjectBoundsSizeCacheX.Values.Min();
        _maxBoundsSizeY = _gameObjectBoundsSizeCacheY.Values.Max();
        _minBoundsSizeY = _gameObjectBoundsSizeCacheY.Values.Min();
        _maxBoundsSizeZ = _gameObjectBoundsSizeCacheZ.Values.Max();
        _minBoundsSizeZ = _gameObjectBoundsSizeCacheZ.Values.Min();
    }

    protected override void OnIterationStart()
    {
        var tags = tagManager.Query<ShelfPlacementRandomizerTag>();
        var placementAreas = tags.Select(tag => tag.GetComponent<Transform>()).ToList();

        foreach (var placementArea in placementAreas)
        {
            var placementAreaPivot = placementArea.parent.transform;
            var faceInstancePos = placementAreaPivot.position;

            while (CheckIfAnyInstanceWillFit(faceInstancePos, placementArea, placementAreaPivot, PlaceDirection.Right))
            {
                var depthInstancePos = faceInstancePos;
                var inDepthProductsCounts = 0;

                var leftSpace = CalculateShelfSpareSpace(faceInstancePos, placementArea, placementAreaPivot, PlaceDirection.Right);           
                var randProduct = RandProduct(leftSpace);
                var instance = InstantiateProduct(placementArea, randProduct);
                var instanceBounds = instance.GetComponent<Renderer>().bounds;
                faceInstancePos = PlaceInstance(instance, instanceBounds, faceInstancePos, PlaceDirection.Right);
                var maxDepthCount = Random.Range(minDepth, maxDepth);

                while (CheckIfInstanceWillFit(instanceBounds, depthInstancePos, placementArea, placementAreaPivot,
                           PlaceDirection.Depth) && inDepthProductsCounts < maxDepthCount )
                {
                    inDepthProductsCounts++;
                    var inStackProductsCounts = 0;
                    
                    var stackInstancePos = depthInstancePos;
                    var dInstance = DuplicateProduct(randProduct, placementArea);
                    depthInstancePos = PlaceInstance(dInstance, instanceBounds, depthInstancePos, PlaceDirection.Depth);
                    var maxStackCount = Random.Range(minStack, maxStack);
                    while (CheckIfInstanceWillFit(instanceBounds, stackInstancePos, placementArea, placementAreaPivot,
                               PlaceDirection.Up) && inStackProductsCounts < maxStackCount)
                    {
                        inStackProductsCounts++;
                        var sInstance = DuplicateProduct(randProduct, placementArea);
                        stackInstancePos = PlaceInstance(sInstance, instanceBounds, stackInstancePos, PlaceDirection.Up);
                    }
                }
            }
        }
    }

    private GameObject DuplicateProduct(GameObject product, Transform parent)
    {
        var dInstance = m_GameObjectOneWayCache.GetOrInstantiate(product);
        dInstance.transform.parent = parent;
        return dInstance;
    }

    private float CalculateShelfSpareSpace(Vector3 startPosition, Transform placementArea, Transform placementAreaPivot, PlaceDirection direction)
    {
        return CalculateAreaSize(placementArea, placementAreaPivot, direction) - startPosition.x;
    }
    
    private GameObject RandProduct(float productMaxWidth)
    {
        var productsThatFit = productMaxWidth <= _maxBoundsSizeX
            ? FindShorterObjects(_gameObjectBoundsSizeCacheX, productMaxWidth)
            : _gameObjectBoundsSizeCacheX.Keys.AsReadOnlyList();

        var randIndex = (int)Mathf.Round((_indexSelector.Sample() * productsThatFit.Count) - 0.5f);
        return productsThatFit[randIndex];
    }


    private GameObject InstantiateProduct(Transform placementArea, GameObject product)
    {
        var instance = m_GameObjectOneWayCache.GetOrInstantiate(product);
        instance.transform.parent = placementArea;
        return instance;
    }

    private IList<GameObject> FindShorterObjects(Dictionary<GameObject, float> gameObjects, float length)
    {
        var objectsThatFit = gameObjects.Where(kvp => kvp.Value < length)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        return objectsThatFit.Keys.AsReadOnlyList();
    }

    private bool CheckIfInstanceWillFit(Bounds bounds, Vector3 prevInstancePos, Transform placementArea,
        Transform placementAreaPivot, PlaceDirection direction)
    {
        return CalculateTotalSize(bounds, prevInstancePos, direction) <
               CalculateAreaSize(placementArea, placementAreaPivot, direction);
    }
    
    private bool CheckIfAnyInstanceWillFit(Vector3 prevInstancePos, Transform placementArea,
        Transform placementAreaPivot, PlaceDirection direction)
    {
        return CalculateMinSeparationDistance(prevInstancePos, direction) <
               CalculateAreaSize(placementArea, placementAreaPivot, direction);
    }

    private bool CheckIfLongestInstanceWillFit(Vector3 prevInstancePos, Transform placementArea,
        Transform placementAreaPivot, PlaceDirection direction)
    {
        return CalculateMaxSeparationDistance(prevInstancePos, direction) <
               CalculateAreaSize(placementArea, placementAreaPivot, direction);
    }

    private float CalculateTotalSize(Bounds bounds, Vector3 position, PlaceDirection direction)
    {
        return direction switch
        {
            PlaceDirection.Right => position.x + bounds.size.x,
            PlaceDirection.Up => position.y + bounds.size.y,
            PlaceDirection.Depth => position.z + bounds.size.z,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }
    private float CalculateMaxSeparationDistance(Vector3 position, PlaceDirection direction)
    {
        return direction switch
        {
            PlaceDirection.Right => position.x + _maxBoundsSizeX,
            PlaceDirection.Up => position.y + _maxBoundsSizeY,
            PlaceDirection.Depth => position.z + _maxBoundsSizeZ,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }

    private float CalculateMinSeparationDistance(Vector3 position, PlaceDirection direction)
    {
        return direction switch
        {
            PlaceDirection.Right => position.x + _minBoundsSizeX,
            PlaceDirection.Up => position.y + _minBoundsSizeY,
            PlaceDirection.Depth => position.z + _minBoundsSizeZ,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }

    [SuppressMessage("ReSharper", "Unity.InefficientPropertyAccess")]
    private float CalculateAreaSize(Transform placementArea, Transform placementAreaPivotTransform, PlaceDirection direction)
    {
        var areaColliderBounds = placementArea.GetComponent<Collider>().bounds;
        return direction switch
        {
            PlaceDirection.Right => areaColliderBounds.size.x + placementAreaPivotTransform.position.x,
            PlaceDirection.Up => areaColliderBounds.size.y + placementAreaPivotTransform.position.y,
            PlaceDirection.Depth => areaColliderBounds.size.z + placementAreaPivotTransform.position.z,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }

    private Vector3 PlaceInstance(GameObject instance, Bounds instanceBounds, Vector3 startPosition, PlaceDirection direction)
    {
        
        var nextInstancePos = CalculateNextInstanceOriginPoint(startPosition, instanceBounds);

        instance.transform.position = nextInstancePos;

        var endOfInstancePosition = new Vector3(startPosition.x, startPosition.y, startPosition.z);
        return direction switch
        {
            PlaceDirection.Right => endOfInstancePosition + new Vector3(instanceBounds.size.x, 0, 0),
            PlaceDirection.Up => endOfInstancePosition + new Vector3(0, instanceBounds.size.y, 0),
            PlaceDirection.Depth => endOfInstancePosition + new Vector3(0, 0, instanceBounds.size.z),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }

    private bool CheckIfInstanceFitsPlacementArea(Transform area, Bounds areaBounds, Vector3 instancePos,
        Bounds instanceBounds)
    {
        var instanceToAreaPosition = area.InverseTransformPoint(
            instancePos + new Vector3(instanceBounds.size.x / 2, instanceBounds.size.y / 2, instanceBounds.size.z / 2));

        return areaBounds.Contains(instanceToAreaPosition);
    }

    private Vector3 CalculateNextInstanceOriginPoint(Vector3 startPosition, Bounds bounds)
    {
        var originPoint = CalculateOriginPoint(bounds);
        return new Vector3(
            startPosition.x + originPoint.x,
            startPosition.y + originPoint.y,
            startPosition.z + originPoint.z);
    }

    private Vector3 CalculateOriginPoint(Bounds bounds)
    {
        return new Vector3(bounds.size.x / 2, bounds.size.y / 2, bounds.size.z / 2);
    }

    private void CalculateProductsLength()
    {
        foreach (var category in prefabs.categories)
        {
            var prefab = category.Item1;

            var renderers = prefab.GetComponentsInChildren<Renderer>();
            var totalBounds = new Bounds();
            foreach (var renderer in renderers)
            {
                totalBounds.Encapsulate(renderer.bounds);
            }
            
            _gameObjectBoundsSizeCacheX.Add(prefab, totalBounds.size.x);
            _gameObjectBoundsSizeCacheY.Add(prefab, totalBounds.size.y);
            _gameObjectBoundsSizeCacheZ.Add(prefab, totalBounds.size.z);
        }
    }

    protected override void OnIterationEnd()
    {
        m_GameObjectOneWayCache.ResetAllObjects();
    }
}