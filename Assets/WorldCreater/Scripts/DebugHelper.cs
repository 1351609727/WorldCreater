using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class DebugHelper
{
    private static DebugHelper _instance;
    public static DebugHelper Instance
    {
        get
        {
            if (_instance == null)
                _instance = new DebugHelper();
            return _instance;
        }
    }
    public bool debugEnable = false;
    StringBuilder debugString = new StringBuilder();

    public void AppendDebug(string str)
    {
        if (debugEnable)
            debugString.AppendLine(str);
    }

    public void PrintDebug()
    {
        if (debugEnable)
        {
            Debug.Log(debugString.ToString());
        }
    }
}
