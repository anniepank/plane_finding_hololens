using Academy.HoloToolkit.Unity;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.SpatialAwareness;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static Academy.HoloToolkit.Unity.PlaneFinding;
using SpatialAwarenessHandler = Microsoft.MixedReality.Toolkit.SpatialAwareness.IMixedRealitySpatialAwarenessObservationHandler<Microsoft.MixedReality.Toolkit.SpatialAwareness.SpatialAwarenessMeshObject>;

public class DemoSpatialMeshHandler : MonoBehaviour, SpatialAwarenessHandler
{
    /// <summary>
    /// Collection that tracks the IDs and count of updates for each active spatial awareness mesh.
    /// </summary>
    private Dictionary<int, uint> meshUpdateData = new Dictionary<int, uint>();

    /// <summary>
    /// Value indicating whether or not this script has registered for spatial awareness events.
    /// </summary>
    private bool isRegistered = false;

    private bool _scanning = false;

    public GameObject SurfacePlanePrefab;
    public GameObject CubePrefab;
    public List<GameObject> ActivePlanes;
    public PlaneTypes drawPlanesMask = (PlaneTypes.Wall | PlaneTypes.Floor | PlaneTypes.Ceiling | PlaneTypes.Table);
    private GameObject planesParent;

    private void Start()
    {
        // RegisterEventHandlers();
        ActivePlanes = new List<GameObject>();
        planesParent = new GameObject("SurfacePlanes");
        planesParent.transform.position = Vector3.zero;
        planesParent.transform.rotation = Quaternion.identity;
    }

    private void Update()
    {
        
    }
    private void MakePlanesRoutine(List<MeshFilter> filters)
    {
        for (int index = 0; index < ActivePlanes.Count; index++)
        {
            Destroy(ActivePlanes[index]);
        }

        float start = Time.realtimeSinceStartup;
        ActivePlanes.Clear();
        // Get the latest Mesh data from the Spatial Mapping Manager.
        List<PlaneFinding.MeshData> meshData = new List<PlaneFinding.MeshData>();

        for (int index = 0; index < filters.Count; index++)
        {
            MeshFilter filter = filters[index];
            if (filter != null && filter.sharedMesh != null)
            {
                // fix surface mesh normals so we can get correct plane orientation.
                filter.mesh.RecalculateNormals();
                meshData.Add(new PlaneFinding.MeshData(filter));
            }
        }

        BoundedPlane[] planes = PlaneFinding.FindPlanes(meshData);

        start = Time.realtimeSinceStartup;

        float maxFloorArea = 0.0f;
        float maxCeilingArea = 0.0f;
        var FloorYPosition = 0.0f;
        var CeilingYPosition = 0.0f;
        float upNormalThreshold = 0.9f;

        // Find the floor and ceiling.
        // We classify the floor as the maximum horizontal surface below the user's head.
        // We classify the ceiling as the maximum horizontal surface above the user's head.
        for (int i = 0; i < planes.Length; i++)
        {
            BoundedPlane boundedPlane = planes[i];
            if (boundedPlane.Bounds.Center.y < 0 && boundedPlane.Plane.normal.y >= upNormalThreshold)
            {
                maxFloorArea = Mathf.Max(maxFloorArea, boundedPlane.Area);
                if (maxFloorArea == boundedPlane.Area)
                {
                    FloorYPosition = boundedPlane.Bounds.Center.y;
                }
            }
        }

        
        // Create SurfacePlane objects to represent each plane found in the Spatial Mapping mesh.
        for (int index = 0; index < planes.Length; index++)
        {
            GameObject destPlane;
            BoundedPlane boundedPlane = planes[index];

            destPlane = Instantiate(CubePrefab);

            destPlane.AddComponent<SurfacePlane>();
            
            destPlane.transform.parent = planesParent.transform;
            
            SurfacePlane surfacePlane = destPlane.GetComponent<SurfacePlane>();
            
            // Set the Plane property to adjust transform position/scale/rotation and determine plane type.
            surfacePlane.Plane = boundedPlane;


            // change here in oreder to select which typs of planes should be drawing
            surfacePlane.IsVisible = true;

            // Set the plane to use the same layer as the SpatialMapping mesh.
            destPlane.layer = 31;
            ActivePlanes.Add(destPlane);
        }

        Debug.Log("Finished making planes.");

    }

    private void OnEnable()
    {
        RegisterEventHandlers();
    }

    private void OnDisable()
    {
        UnregisterEventHandlers();
    }

    private void OnDestroy()
    {
        UnregisterEventHandlers();
    }

    /// <summary>
    /// Registers for the spatial awareness system events.
    /// </summary>
    private void RegisterEventHandlers()
    {
        if (!isRegistered && (CoreServices.SpatialAwarenessSystem != null))
        {
            CoreServices.SpatialAwarenessSystem.RegisterHandler<SpatialAwarenessHandler>(this);
            isRegistered = true;
        }
    }

    /// <summary>
    /// Unregisters from the spatial awareness system events.
    /// </summary>
    private void UnregisterEventHandlers()
    {
        if (isRegistered && (CoreServices.SpatialAwarenessSystem != null))
        {
            CoreServices.SpatialAwarenessSystem.UnregisterHandler<SpatialAwarenessHandler>(this);
            isRegistered = false;
        }
    }

    /// <inheritdoc />
    public virtual void OnObservationAdded(MixedRealitySpatialAwarenessEventData<SpatialAwarenessMeshObject> eventData)
    {
        // A new mesh has been added.
        if (!meshUpdateData.ContainsKey(eventData.Id))
        {
            Debug.Log($"Tracking mesh {eventData.Id}");
            meshUpdateData.Add(eventData.Id, 0);
        }
    }

    /// <inheritdoc />
    public virtual void OnObservationUpdated(MixedRealitySpatialAwarenessEventData<SpatialAwarenessMeshObject> eventData)
    {
        uint updateCount = 0;

        // A mesh has been updated. Find it and increment the update count.
        if (meshUpdateData.TryGetValue(eventData.Id, out updateCount))
        {
            // Set the new update count.
            meshUpdateData[eventData.Id] = ++updateCount;

            Debug.Log($"Mesh {eventData.Id} has been updated {updateCount} times.");
        }
    }

    /// <inheritdoc />
    public virtual void OnObservationRemoved(MixedRealitySpatialAwarenessEventData<SpatialAwarenessMeshObject> eventData)
    {
        // A mesh has been removed. We no longer need to track the count of updates.
        if (meshUpdateData.ContainsKey(eventData.Id))
        {
            Debug.Log($"No longer tracking mesh {eventData.Id}.");
            meshUpdateData.Remove(eventData.Id);
        }
    }

    
    public void ShowMesh()
    {
        _scanning = !_scanning;

        if (_scanning)
        {
            var spatialAwarenessService = CoreServices.SpatialAwarenessSystem;

            // Cast to the IMixedRealityDataProviderAccess to get access to the data providers
            var dataProviderAccess = spatialAwarenessService as IMixedRealityDataProviderAccess;

            // Get the first Mesh Observer available, generally we have only one registered
            var meshObserver = dataProviderAccess.GetDataProvider<IMixedRealitySpatialAwarenessMeshObserver>();

            // Get the SpatialObjectMeshObserver specifically
            var meshObserverName = "Spatial Object Mesh Observer";
            var spatialObjectMeshObserver = dataProviderAccess.GetDataProvider<IMixedRealitySpatialAwarenessMeshObserver>(meshObserverName);
            // Cast the Spatial Awareness system to IMixedRealityDataProviderAccess to get an Observer
            var access = CoreServices.SpatialAwarenessSystem as IMixedRealityDataProviderAccess;

            // Get the first Mesh Observer available, generally we have only one registered
            var observer = access.GetDataProvider<IMixedRealitySpatialAwarenessMeshObserver>();

            var meshes = new List<MeshFilter>();

            foreach (SpatialAwarenessMeshObject meshObject in observer.Meshes.Values)
            {
                Mesh mesh = meshObject.Filter.mesh;
                // Do something with the Mesh object
                meshes.Add(meshObject.Filter);
            }

            MakePlanesRoutine(meshes);
        }


    }

    public void HideMesh()
    {
        // Cast the Spatial Awareness system to IMixedRealityDataProviderAccess to get an Observer
        var access = CoreServices.SpatialAwarenessSystem as IMixedRealityDataProviderAccess;

        // Get the first Mesh Observer available, generally we have only one registered
        var observer = access.GetDataProvider<IMixedRealitySpatialAwarenessMeshObserver>();

        // Set to not visible
        observer.DisplayOption = SpatialAwarenessMeshDisplayOptions.None;
    }
}
