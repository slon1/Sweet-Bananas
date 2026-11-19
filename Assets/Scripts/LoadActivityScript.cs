using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadActivityScript : MonoBehaviour
{
    [SerializeField] private GameObject spin;
    [SerializeField] private string scene;
    [SerializeField] private bool clock;
    private int progress_percent;

    private void Start()
    {
        
        InitSpinProgressPercent();
    }

    private async void InitSpinProgressPercent()
    {
        while (progress_percent < 95)
        {
            ChangeLoadProgress();
            await Task.Delay(TimeSpan.FromSeconds(0.01f));
        }
        await Task.Delay(TimeSpan.FromSeconds(0.2f));
        while (progress_percent < 100)
        {
            ChangeLoadProgress();
            await Task.Delay(TimeSpan.FromSeconds(0.01f));
        }
        await Task.Delay(TimeSpan.FromSeconds(0.2f));
        SceneManager.LoadScene(scene);
    }

    private void ChangeLoadProgress()
    {
        spin.transform.Rotate(new Vector3(0f, 0f, clock ? -5f : 5f));
        progress_percent++;
    }
}
