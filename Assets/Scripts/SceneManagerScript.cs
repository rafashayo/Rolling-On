using UnityEngine;
using UnityEngine.SceneManagement; 

public class SceneManagerScript : MonoBehaviour
{
    public static SceneManagerScript Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
        DontDestroyOnLoad(this);
    }

    // Update is called once per frame
    public void IrACrear(string Joinearse)
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("Joinearse");
    }

}
