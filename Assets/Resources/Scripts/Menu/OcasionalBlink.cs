using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OcasionalBlink : MonoBehaviour
{
    [Header("Intervalo de tempo (segundos)")]
    [SerializeField] private float minTime = 3f;
    [SerializeField] private float maxTime = 8f;

    [Header("Animator")]
    [SerializeField] private Animator animator;

    void Start()
    {
        StartCoroutine(PlayBlink());
    }

    IEnumerator PlayBlink()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minTime, maxTime));

            while (animator.IsInTransition(0))
            {
                yield return null;
            }

            ResetAllTriggers();
            animator.SetTrigger("Blink");
        }
    }

    void ResetAllTriggers()
    {
        animator.ResetTrigger("Blink");
    }
}
