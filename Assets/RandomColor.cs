using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomColor : MonoBehaviour
{
    public Gradient Gradient;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Color c = Gradient.Evaluate(Mathf.Sin(Time.time) * 0.5f + 0.5f);
        Debug.Log(c);
        GetComponent<Light>().color = c;
        
        
    }
}
