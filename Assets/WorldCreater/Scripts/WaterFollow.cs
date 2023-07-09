using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterFollow : MonoBehaviour
{
    public GameObject water;
    public Camera camera;
    public float scaleMulit = 0.001f;
    public float waterBaseHeight;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float height = camera.transform.position.y;
        float scaler = height * scaleMulit + 1;
        water.transform.localScale = new Vector3(scaler, scaler, scaler);

        Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 1));
        Plane raycastPlane = new Plane(Vector3.up, -1 * waterBaseHeight);
        float enter;
        raycastPlane.Raycast(ray, out enter);
        water.transform.position = ray.GetPoint(enter);
    }
}
