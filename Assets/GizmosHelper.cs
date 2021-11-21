using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[ExecuteInEditMode]
public class GizmosHelper : MonoBehaviour
{
    public Action OnGizmos;

    private void OnDestroy()
    {
        OnGizmos = null;
    }

    private void OnDrawGizmos()
    {
        if(OnGizmos != null)
        {
            OnGizmos();
        }
    }
}
