using System;
using System.Collections.Generic;
using UnityEngine;

public class SpatialHash
{
    private float spacing;
    private int tableSize;
    private int[] cellStart;
    private int[] cellEntries;
    private int[] queryIds;
    private int querySize;
    public SpatialHash(float spacing, int maxNumObjects)
    {
        this.spacing = spacing;
        this.tableSize = 2 * maxNumObjects;
        this.cellStart = new int[this.tableSize + 1];
        this.cellEntries = new int[maxNumObjects];
        this.queryIds = new int[maxNumObjects];
        this.querySize = 0;
    }

    private int HashCoords(int xi, int yi, int zi)
    {
        int h = (xi * 92837111) ^ (yi * 689287499) ^ (zi * 283923481); // fantasy function
        return Mathf.Abs(h) % tableSize;
    }

    private int IntCoord(float coord)
    {
        return Mathf.FloorToInt(coord / spacing);
    }

    private int HashPos(Vector3 pos)
    {
        return HashCoords(IntCoord(pos.x), IntCoord(pos.y), IntCoord(pos.z));
    }

    public void Create(Vector3[] positions)
    {
        int numObjects = Mathf.Min(positions.Length, cellEntries.Length);

        // Determine cell sizes
        Array.Clear(cellStart, 0, cellStart.Length);
        Array.Clear(cellEntries, 0, cellEntries.Length);

        for (int i = 0; i < numObjects; i++)
        {
            int h = HashPos(positions[i]);
            cellStart[h]++;
        }

        // Determine cells starts
        int start = 0;
        for (int i = 0; i < tableSize; i++)
        {
            start += cellStart[i];
            cellStart[i] = start;
        }
        cellStart[tableSize] = start; // guard

        // Fill in objects ids
        for (int i = 0; i < numObjects; i++)
        {
            int h = HashPos(positions[i]);
            cellStart[h]--;
            cellEntries[cellStart[h]] = i;
        }
    }
    public void Query(Vector3 pos, float maxDist)
    {
        int x0 = IntCoord(pos.x - maxDist);
        int y0 = IntCoord(pos.y - maxDist);
        int z0 = IntCoord(pos.z - maxDist);

        int x1 = IntCoord(pos.x + maxDist);
        int y1 = IntCoord(pos.y + maxDist);
        int z1 = IntCoord(pos.z + maxDist);

        querySize = 0;

        for (int xi = x0; xi <= x1; xi++)
        {
            for (int yi = y0; yi <= y1; yi++)
            {
                for (int zi = z0; zi <= z1; zi++)
                {
                    int h = HashCoords(xi, yi, zi);
                    int start = cellStart[h];
                    int end = cellStart[h + 1];

                    for (int i = start; i < end; i++)
                    {
                        queryIds[querySize] = cellEntries[i];
                        querySize++;
                    }
                }
            }
        }
    }

    public int QuerySize => querySize;
    public int[] QueryIds => queryIds;
}
