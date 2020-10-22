using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using UnityEngine;
//using HDF5CSharp;
using HDF.PInvoke;




public class BoidManager : MonoBehaviour {

    const int threadGroupSize = 1024;

    public BoidSettings settings;
    public ComputeShader compute;
    Boid[] boids;

    void Start () {
        string datasetPath = "/home/hugo/MA3/LIS/visual_swarm/data/phase_diagram/logs/alpha0_10_00_beta0_10_00.hdf5";
        boids = FindObjectsOfType<Boid> ();
        foreach (Boid b in boids) {
            b.Initialize (settings, null);
        }
        /*List<string> datasetNames = new List<string>();
        List<string> groupNames = new List<string>();
        long fileId = H5F.open(datasetPath, H5F.ACC_RDONLY);
        var rootId = H5G.open(fileId, "/");

        H5O.visit(fileId, H5.index_t.NAME, H5.iter_order_t.INC, new H5O.iterate_t(
        delegate(int objectId, IntPtr namePtr, ref H5O.info_t info, IntPtr op_data)
        {
          string objectName = Marshal.PtrToStringAnsi(namePtr);
          H5O.info_t gInfo = new H5O.info_t();
          H5O.get_info_by_name(objectId, objectName, ref gInfo);

          if (gInfo.type == H5O.type_t.DATASET)
          {
            datasetNames.Add(objectName);
          }
          else if (gInfo.type == H5O.type_t.GROUP)
          {
            groupNames.Add(objectName);
          }
          return 0;
        }), new IntPtr());
      
        H5G.close(rootId);*/

        // Print out the information that we found
        foreach (var line in datasetNames)
        {
            Debug.WriteLine(line);
        }
       / List<float[]> dataset = new List<float[]>();
        long fileId = H5F.open(datasetPath, H5F.ACC_RDONLY);
        /*long dataSetId = H5D.open(fileID, "/state/position");
        long typeId = H5D.get_type(dataSetId);
        int width = 1920, height = 1080;
        float[] data = new float[ width * height ];
        GCHandle gch = GCHandle.Alloc( data, GCHandleType.Pinned );*/
        long dataSetId = 0;
        long dataSpaceId = 0;
        int recordLength = 1000;
        try
        {
            dataSetId = H5D.open(fileId, "/state/position");
            dataSpaceId = H5D.get_space(dataSetId);
            long typeId = H5T.copy(H5T.C_S1);
            H5T.set_size(typeId, new IntPtr(recordLength));
            

            int rank = H5S.get_simple_extent_ndims(dataSpaceId);
            ulong[] dims = new ulong[rank];
            ulong[] maxDims = new ulong[rank];
            H5S.get_simple_extent_dims(dataSpaceId, dims, maxDims);
            float[] dataBytes = new float[dims[0] * (ulong)recordLength];

            GCHandle pinnedArray = GCHandle.Alloc(dataBytes, GCHandleType.Pinned);
            H5D.read(dataSetId, typeId, H5S.ALL, H5S.ALL, H5P.DEFAULT, pinnedArray.AddrOfPinnedObject());
            pinnedArray.Free();
            dataset.Add(dataBytes);
        }
        finally
        {
            if (dataSpaceId != 0) H5S.close(dataSpaceId);
            if (dataSetId != 0) H5D.close(dataSetId);
        }
        //datasetOut = dataset.ToArray();
    }

    void Update () {
        if (boids != null) {

            int numBoids = boids.Length;
            var boidData = new BoidData[numBoids];

            for (int i = 0; i < boids.Length; i++) {
                boidData[i].position = boids[i].position;
                boidData[i].direction = boids[i].forward;
            }

            var boidBuffer = new ComputeBuffer (numBoids, BoidData.Size);
            boidBuffer.SetData (boidData);

            compute.SetBuffer (0, "boids", boidBuffer);
            compute.SetInt ("numBoids", boids.Length);
            compute.SetFloat ("viewRadius", settings.perceptionRadius);
            compute.SetFloat ("avoidRadius", settings.avoidanceRadius);

            int threadGroups = Mathf.CeilToInt (numBoids / (float) threadGroupSize);
            compute.Dispatch (0, threadGroups, 1, 1);

            boidBuffer.GetData (boidData);

            for (int i = 0; i < boids.Length; i++) {
                boids[i].avgFlockHeading = boidData[i].flockHeading;
                boids[i].centreOfFlockmates = boidData[i].flockCentre;
                boids[i].avgAvoidanceHeading = boidData[i].avoidanceHeading;
                boids[i].numPerceivedFlockmates = boidData[i].numFlockmates;

                boids[i].UpdateBoid ();
            }

            boidBuffer.Release ();
        }
    }

    public struct BoidData {
        public Vector3 position;
        public Vector3 direction;

        public Vector3 flockHeading;
        public Vector3 flockCentre;
        public Vector3 avoidanceHeading;
        public int numFlockmates;

        public static int Size {
            get {
                return sizeof (float) * 3 * 5 + sizeof (int);
            }
        }
    }
}