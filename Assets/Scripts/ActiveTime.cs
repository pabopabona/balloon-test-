using System.Collections;
using UnityEngine;

public class ActiveTime : MonoBehaviour
{
    public string Comment = "Unable Itself Instead Use Child";
    public float activeTime = 1;
    public GameObject ActiveObj;

    void OnEnable()
    {
        if (activeTime == 0)
        {
            Active();
        }
        else
        {
            StartCoroutine(ActiveInTime());
        }
    }

    void Active()
    {
        ActiveObj.SetActive(true);
    }

    IEnumerator ActiveInTime()
    {
        yield return new WaitForSeconds(activeTime);
        Active();
    }
}
