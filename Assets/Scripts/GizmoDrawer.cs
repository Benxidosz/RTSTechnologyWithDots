using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public class GizmoDrawer : MonoBehaviour {
    [SerializeField] private float mapSize;

    [SerializeField] private float rStep;
    [SerializeField] private float angleStepInDegree;

    private void OnDrawGizmos() {
        Handles.color = Color.red;
        if (rStep == 0f)
            rStep = 1f;
        if (angleStepInDegree == 0f)
            angleStepInDegree = 1f;
        for (float r = rStep; r <= mapSize; r += rStep) {
            Handles.DrawWireDisc(transform.position, Vector3.up, r);
        }

        for (float angle = 0; angle <= 2 * math.PI; angle += math.radians(angleStepInDegree)) {
            Handles.DrawLine(transform.position,
                new Vector3(math.cos(angle) * mapSize, transform.position.y, math.sin(angle) * mapSize));
        }
    }
}