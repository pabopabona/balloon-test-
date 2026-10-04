using System.Collections;
using UnityEngine;

public class DeActiveTime : MonoBehaviour
{
    public float deactiveTime = 1;
    public GameObject targetObj;

    void Start()
    {
        if (deactiveTime == 0)
        {
            targetObj = gameObject;
        }
        else
        {
            StartCoroutine(DeActiveInTime());
        }
    }

    IEnumerator DeActiveInTime()
    {
        if (deactiveTime > 0)
        {
            yield return new WaitForSeconds(deactiveTime);
        }
        targetObj.SetActive(false);
    }
}
