using System;
using System.Collections.Generic;
using UnityEngine;

public class AnimParm : MonoBehaviour
{
    public Animator animator;
    public List<animPair> animPairsFloat = new List<animPair>();

    void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();


    }

    // Update is called once per frame
    void Update()
    {
        foreach(var v in animPairsFloat)
        {
            animator.SetFloat(v.key, v.Value);   
        }    
    }


    [Serializable]
    public struct animPair { 
        public string key;
        [Range(-0.999f, 0.999f)] public float Value;
    }
}
