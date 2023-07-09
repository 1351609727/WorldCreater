using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Flythrough : MonoBehaviour
{
    float lookSpeed = 1.0f;
    float moveSpeed = 0.07f;

    public GameObject sun;
    public Camera mycamera;
    private Quaternion baseSunTransform;
    public float sunX;
    public float sunY;
    public float sunZ;
    public float defaultFov;
    public float baseSpeed = 0.07f;
    public float runSpeed = 0.3f;
    public float dashSpeed = 2f;
    // Start is called before the first frame update
    void Start()
    {
        //mycamera.fieldOfView = defaultFov;
        //Cursor.lockState = CursorLockMode.Confined;
        //Cursor.visible = false;
        baseSunTransform = sun.transform.rotation;
    }
   

    // Update is called once per frame
    void Update()
    {

        //if (Input.GetKey("e"))
        //{
        //    sun.transform.Rotate(0, 0.1f, 0,Space.World);
        //}
        if (Input.GetKey("q"))
        {
            sun.transform.Rotate(0, -0.1f, 0, Space.World);
        }
        //if (Input.GetKey("z"))
        //{
        //   // sun.transform.Rotate(-0.1f, 0, 0, Space.World);
        //}
        //if (Input.GetKey("x"))
        //{
        //  //  sun.transform.Rotate(0.1f, 0, 0, Space.World);
        //}
        if (Input.GetKeyDown("t"))
        {
            sun.transform.eulerAngles = new Vector3(sunX, sunY, sunZ);
        }
        if (Input.GetKey("v"))
        {
            mycamera.fieldOfView -=0.3f;
        }
        if (Input.GetKey("b"))
        {
            mycamera.fieldOfView += 0.3f;
        }
        if (Input.GetKeyDown("n"))
        {
            mycamera.fieldOfView = defaultFov;
        }
        if (Input.GetKey(KeyCode.LeftShift))
        {
            moveSpeed = runSpeed;
        }
        else if (Input.GetKey(KeyCode.LeftAlt))
        {
            moveSpeed = dashSpeed;
        }
        else
        {
            moveSpeed = baseSpeed;
        }

        Vector3 srcPosition = transform.position;
        Vector3 forward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;

        if (Input.GetAxis("Vertical")!=0 && Input.GetAxis("Horizontal") !=0)
        {
            transform.position += forward * moveSpeed/1.5f * Input.GetAxis("Vertical") * Time.deltaTime;
            transform.position += transform.right * moveSpeed/ 1.5f * Input.GetAxis("Horizontal") * Time.deltaTime;
        } else
        {
            transform.position += forward * moveSpeed * Input.GetAxis("Vertical") * Time.deltaTime;
            transform.position += transform.right * moveSpeed * Input.GetAxis("Horizontal") * Time.deltaTime;
        }
 
        if (Input.GetKey(KeyCode.X))
        {
            transform.position += Vector3.up * moveSpeed * 0.25f * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.Z))
        {
            transform.position += Vector3.up * moveSpeed * -0.25f * Time.deltaTime;
        }

        if (transform.position.y > 100 || transform.position.y < 30)
            transform.position = srcPosition;
    }
}
