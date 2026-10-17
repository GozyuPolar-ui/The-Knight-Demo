using UnityEngine;
using UnityEngine.SceneManagement;

public class Portal : MonoBehaviour
{
    [SerializeField]
    private string sceneName; 

    [SerializeField] private float humRange = 12f;   // distance (world units) at which the hum becomes inaudible
    [SerializeField] private float humVolume = 0.6f;

    private AudioSource humSource;
    private Transform player;

    private void Start()
    {
        AudioClip clip = AudioManager.GetClip(Sfx.PortalHum);
        if (clip == null)
        {
            return;
        }

        humSource = gameObject.AddComponent<AudioSource>();
        humSource.clip = clip;
        humSource.loop = true;
        humSource.playOnAwake = false;
        humSource.spatialBlend = 0f;
        humSource.volume = 0f;
        humSource.Play();

        GameObject knight = GameObject.FindWithTag("Player");
        if (knight != null)
        {
            player = knight.transform;
        }
    }

    // The hum gets louder the closer the player is to the portal.
    private void Update()
    {
        if (humSource == null)
        {
            return;
        }

        if (player == null)
        {
            humSource.volume = 0f;
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);
        humSource.volume = AudioManager.SfxVolume * humVolume * Mathf.Clamp01(1f - distance / humRange);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            AudioManager.Play(Sfx.Teleport);
            SceneManager.LoadScene(sceneName);
        }
    }
}
