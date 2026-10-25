using System.Linq;
using UnityEngine;

public class FirstPersonAudio : MonoBehaviour
{
    public FirstPersonMovement character;
    public GroundCheck groundCheck;

    [Header("Pasos")]
    public AudioSource stepAudio;
    public AudioSource runningAudio;

    public float velocityThreshold = 0.01f;

    private Vector2 lastCharacterPosition;
    private Vector2 CurrentCharacterPosition => character ? new Vector2(character.transform.position.x, character.transform.position.z) : Vector2.zero;

    [Header("Aterrizaje")]
    public AudioSource landingAudio;
    public AudioClip[] landingSFX;

    [Header("Salto")]
    public Jump jump;
    public AudioSource jumpAudio;
    public AudioClip[] jumpSFX;

    [Header("Agacharse")]
    public Crouch crouch;
    public AudioSource crouchStartAudio, crouchedAudio, crouchEndAudio;
    public AudioClip[] crouchStartSFX, crouchEndSFX;

    private AudioSource[] movingAudios;

    private void Reset()
    {
        character = GetComponentInParent<FirstPersonMovement>();
        groundCheck = (transform.parent ?? transform).GetComponentInChildren<GroundCheck>();
        stepAudio = GetOrCreateAudioSource("Step Audio");
        runningAudio = GetOrCreateAudioSource("Running Audio");
        landingAudio = GetOrCreateAudioSource("Landing Audio");

        jump = GetComponentInParent<Jump>();
        if (jump != null)
        {
            jumpAudio = GetOrCreateAudioSource("Jump audio");
        }

        crouch = GetComponentInParent<Crouch>();
        if (crouch != null)
        {
            crouchStartAudio = GetOrCreateAudioSource("Crouch Start Audio");
            crouchedAudio = GetOrCreateAudioSource("Crouched Audio");
            crouchEndAudio = GetOrCreateAudioSource("Crouch End Audio");
        }
    }

    private void Awake()
    {
        if (character == null) character = GetComponentInParent<FirstPersonMovement>();
        if (groundCheck == null) groundCheck = (transform.parent ?? transform).GetComponentInChildren<GroundCheck>();
        if (jump == null) jump = GetComponentInParent<Jump>();
        if (crouch == null) crouch = GetComponentInParent<Crouch>();

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

        if (jumpAudio == null)
        {
            jumpAudio = GetOrCreateAudioSource("Jump Audio");
        }
        if (jumpAudio != null)
        {
            jumpAudio.spatialBlend = 0f;
        }

        movingAudios = new AudioSource[] { stepAudio, runningAudio, crouchedAudio };

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

    private void OnEnable() => SubscribeToEvents();

    private void OnDisable() => UnsubscribeToEvents();

    private void FixedUpdate()
    {
        if (character == null) return;

        float velocity = Vector3.Distance(CurrentCharacterPosition, lastCharacterPosition);
        bool isGrounded = (groundCheck != null && groundCheck.isGrounded) || character.IsGrounded();

        if (velocity >= velocityThreshold && isGrounded)
        {
            bool isCrouched = (crouch != null && crouch.IsCrouched) || character.IsCrouched;
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

    private void SetPlayingMovingAudio(AudioSource audioToPlay)
    {
        if (movingAudios == null) return;

        for (int i = 0; i < movingAudios.Length; i++)
        {
            AudioSource a = movingAudios[i];
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

    private void PlayLandingAudio() => PlayRandomClip(landingAudio, landingSFX);
    private void PlayJumpAudio() => PlayRandomClip(jumpAudio, jumpSFX);
    private void PlayCrouchStartAudio() => PlayRandomClip(crouchStartAudio, crouchStartSFX);
    private void PlayCrouchEndAudio() => PlayRandomClip(crouchEndAudio, crouchEndSFX);

    private void SubscribeToEvents()
    {
        if (groundCheck != null) groundCheck.Grounded += PlayLandingAudio;

        if (jump != null)
        {
            jump.Jumped += PlayJumpAudio;
        }
        else if (character != null)
        {
            character.Jumped += PlayJumpAudio;
        }

        if (crouch != null)
        {
            crouch.CrouchStart += PlayCrouchStartAudio;
            crouch.CrouchEnd += PlayCrouchEndAudio;
        }
        else if (character != null)
        {
            character.CrouchStart += PlayCrouchStartAudio;
            character.CrouchEnd += PlayCrouchEndAudio;
        }
    }

    private void UnsubscribeToEvents()
    {
        if (groundCheck != null) groundCheck.Grounded -= PlayLandingAudio;

        if (jump != null)
        {
            jump.Jumped -= PlayJumpAudio;
        }
        else if (character != null)
        {
            character.Jumped -= PlayJumpAudio;
        }

        if (crouch != null)
        {
            crouch.CrouchStart -= PlayCrouchStartAudio;
            crouch.CrouchEnd -= PlayCrouchEndAudio;
        }
        else if (character != null)
        {
            character.CrouchStart -= PlayCrouchStartAudio;
            character.CrouchEnd -= PlayCrouchEndAudio;
        }
    }

    private AudioSource GetOrCreateAudioSource(string name)
    {
        AudioSource result = System.Array.Find(GetComponentsInChildren<AudioSource>(), a => a.name == name);
        if (result != null) return result;

        result = new GameObject(name).AddComponent<AudioSource>();
        result.spatialBlend = 1f;
        result.playOnAwake = false;
        result.transform.SetParent(transform, false);
        return result;
    }

    private static void PlayRandomClip(AudioSource audio, AudioClip[] clips)
    {
        if (audio == null || clips == null || clips.Length <= 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clips.Length > 1)
        {
            while (clip == audio.clip)
            {
                clip = clips[Random.Range(0, clips.Length)];
            }
        }

        audio.clip = clip;
        audio.Play();
    }
}
