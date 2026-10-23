using System.Linq;
using UnityEngine;

public class FirstPersonAudio : MonoBehaviour
{
    public FirstPersonMovement character;
    public GroundCheck groundCheck;

    [Header("Step")]
    public AudioSource stepAudio;
    public AudioSource runningAudio;
    [Tooltip("Minimum velocity for moving audio to play")]
    public float velocityThreshold = .01f;
    Vector2 lastCharacterPosition;
    Vector2 CurrentCharacterPosition => character ? new Vector2(character.transform.position.x, character.transform.position.z) : Vector2.zero;

    [Header("Landing")]
    public AudioSource landingAudio;
    public AudioClip[] landingSFX;

    [Header("Jump")]
    public Jump jump;
    public AudioSource jumpAudio;
    public AudioClip[] jumpSFX;

    [Header("Crouch")]
    public Crouch crouch;
    public AudioSource crouchStartAudio, crouchedAudio, crouchEndAudio;
    public AudioClip[] crouchStartSFX, crouchEndSFX;

    AudioSource[] MovingAudios => new AudioSource[] { stepAudio, runningAudio, crouchedAudio };

    void Reset()
    {
        character = GetComponentInParent<FirstPersonMovement>();
        groundCheck = (transform.parent ?? transform).GetComponentInChildren<GroundCheck>();
        stepAudio = GetOrCreateAudioSource("Step Audio");
        runningAudio = GetOrCreateAudioSource("Running Audio");
        landingAudio = GetOrCreateAudioSource("Landing Audio");

        jump = GetComponentInParent<Jump>();
        if (jump)
        {
            jumpAudio = GetOrCreateAudioSource("Jump audio");
        }

        crouch = GetComponentInParent<Crouch>();
        if (crouch)
        {
            crouchStartAudio = GetOrCreateAudioSource("Crouch Start Audio");
            crouchedAudio = GetOrCreateAudioSource("Crouched Audio");
            crouchEndAudio = GetOrCreateAudioSource("Crouch End Audio");
        }
    }

    void Awake()
    {
        if (!character) character = GetComponentInParent<FirstPersonMovement>();
        if (!groundCheck) groundCheck = (transform.parent ?? transform).GetComponentInChildren<GroundCheck>();
        if (!jump) jump = GetComponentInParent<Jump>();
        if (!crouch) crouch = GetComponentInParent<Crouch>();

        if (stepAudio != null)
        {
            stepAudio.loop = true;
            stepAudio.spatialBlend = 0f;
        }

        if (runningAudio != null)
        {
            runningAudio.loop = true;
            runningAudio.spatialBlend = 0f;
        }

        if (crouchedAudio != null)
        {
            crouchedAudio.loop = true;
            crouchedAudio.spatialBlend = 0f;
        }

        if (landingAudio != null)
        {
            landingAudio.spatialBlend = 0f;
        }

        if (!jumpAudio)
        {
            jumpAudio = GetOrCreateAudioSource("Jump Audio");
        }
        if (jumpAudio != null)
        {
            jumpAudio.spatialBlend = 0f;
        }

        // Auto-cargar clips si los arrays vinieron vacíos del prefab
        if (jumpSFX == null || jumpSFX.Length == 0)
        {
            jumpSFX = Resources.FindObjectsOfTypeAll<AudioClip>()
                .Where(c => c != null && c.name.ToLower().StartsWith("jump"))
                .ToArray();
        }

        if (crouchStartSFX == null || crouchStartSFX.Length == 0)
        {
            crouchStartSFX = Resources.FindObjectsOfTypeAll<AudioClip>()
                .Where(c => c != null && c.name.ToLower().Contains("crouch start"))
                .ToArray();
        }

        if (crouchEndSFX == null || crouchEndSFX.Length == 0)
        {
            crouchEndSFX = Resources.FindObjectsOfTypeAll<AudioClip>()
                .Where(c => c != null && c.name.ToLower().Contains("crouch end"))
                .ToArray();
        }
    }

    void OnEnable() => SubscribeToEvents();

    void OnDisable() => UnsubscribeToEvents();

    void FixedUpdate()
    {
        if (!character) return;

        // Play moving audio if the character is moving and on the ground.
        float velocity = Vector3.Distance(CurrentCharacterPosition, lastCharacterPosition);
        bool isGrounded = (groundCheck && groundCheck.isGrounded) || (character != null && character.IsGrounded());

        if (velocity >= velocityThreshold && isGrounded)
        {
            bool isCrouched = (crouch != null && crouch.IsCrouched) || (character != null && character.IsCrouched);
            if (isCrouched)
            {
                if (crouchedAudio != null)
                {
                    SetPlayingMovingAudio(crouchedAudio);
                }
                else if (stepAudio != null)
                {
                    stepAudio.pitch = 0.75f;
                    SetPlayingMovingAudio(stepAudio);
                }
            }
            else if (character.IsRunning)
            {
                if (runningAudio != null)
                {
                    SetPlayingMovingAudio(runningAudio);
                }
                else if (stepAudio != null)
                {
                    // Fallback para correr: pasos a mayor ritmo y pitch
                    stepAudio.pitch = 1.35f;
                    SetPlayingMovingAudio(stepAudio);
                }
            }
            else
            {
                if (stepAudio != null)
                {
                    stepAudio.pitch = 1.0f;
                    SetPlayingMovingAudio(stepAudio);
                }
            }
        }
        else
        {
            SetPlayingMovingAudio(null);
        }

        lastCharacterPosition = CurrentCharacterPosition;
    }

    void SetPlayingMovingAudio(AudioSource audioToPlay)
    {
        AudioSource[] audios = MovingAudios;
        for (int i = 0; i < audios.Length; i++)
        {
            AudioSource a = audios[i];
            if (a != null && a != audioToPlay && a.isPlaying)
            {
                a.Pause();
            }
        }

        if (audioToPlay != null && !audioToPlay.isPlaying)
        {
            audioToPlay.Play();
        }
    }

    #region Play instant-related audios.
    void PlayLandingAudio() => PlayRandomClip(landingAudio, landingSFX);
    void PlayJumpAudio() => PlayRandomClip(jumpAudio, jumpSFX);
    void PlayCrouchStartAudio() => PlayRandomClip(crouchStartAudio, crouchStartSFX);
    void PlayCrouchEndAudio() => PlayRandomClip(crouchEndAudio, crouchEndSFX);
    #endregion

    #region Subscribe/unsubscribe to events.
    void SubscribeToEvents()
    {
        if (groundCheck) groundCheck.Grounded += PlayLandingAudio;

        if (jump)
        {
            jump.Jumped += PlayJumpAudio;
        }
        else if (character)
        {
            character.Jumped += PlayJumpAudio;
        }

        if (crouch)
        {
            crouch.CrouchStart += PlayCrouchStartAudio;
            crouch.CrouchEnd += PlayCrouchEndAudio;
        }
        else if (character)
        {
            character.CrouchStart += PlayCrouchStartAudio;
            character.CrouchEnd += PlayCrouchEndAudio;
        }
    }

    void UnsubscribeToEvents()
    {
        if (groundCheck) groundCheck.Grounded -= PlayLandingAudio;

        if (jump)
        {
            jump.Jumped -= PlayJumpAudio;
        }
        else if (character)
        {
            character.Jumped -= PlayJumpAudio;
        }

        if (crouch)
        {
            crouch.CrouchStart -= PlayCrouchStartAudio;
            crouch.CrouchEnd -= PlayCrouchEndAudio;
        }
        else if (character)
        {
            character.CrouchStart -= PlayCrouchStartAudio;
            character.CrouchEnd -= PlayCrouchEndAudio;
        }
    }
    #endregion

    #region Utility.
    AudioSource GetOrCreateAudioSource(string name)
    {
        AudioSource result = System.Array.Find(GetComponentsInChildren<AudioSource>(), a => a.name == name);
        if (result)
            return result;

        result = new GameObject(name).AddComponent<AudioSource>();
        result.spatialBlend = 1;
        result.playOnAwake = false;
        result.transform.SetParent(transform, false);
        return result;
    }

    static void PlayRandomClip(AudioSource audio, AudioClip[] clips)
    {
        if (!audio || clips == null || clips.Length <= 0)
            return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clips.Length > 1)
            while (clip == audio.clip)
                clip = clips[Random.Range(0, clips.Length)];

        audio.clip = clip;
        audio.Play();
    }
    #endregion 
}
