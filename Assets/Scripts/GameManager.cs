﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public class GameManager : MonoBehaviour
{

    public static GameManager gm;
    public static MapManager mm = null;
    public static PlayManager pm = null;

    public enum GravityDirection { Up, Down, Left, Right }

    [HideInInspector]
    public bool canPlay = true;

    private enum AdventureLevel { NULL = 0, Easy = 1, Normal = 2, Hard = 3, Insane = 4 }

    [SerializeField]
    private AdventureLevel adventureLevel;
    private int playingMapIndex = 0;

    public int PlayingMapIndex{
        get{return playingMapIndex;}
    }

    public GameObject floorStarPrefab;

    public AudioSource bgmAudioSource;
    public List<AudioClip> bgms;
    public float bgmVolume = 0.8f;
    private List<float> bgmVolumeForEach = new List<float>() { 1f, 0.7f, 1f };

    public AudioSource sfxAudioSource;
    public List<AudioClip> ballSfxs;
    public List<AudioClip> ironSfxs;
    public AudioClip wallSfx;
    public AudioClip shutterSfx;
    public AudioClip squashedSfx;
    public AudioClip burnedSfx;
    public AudioClip escapedSfx;
    public AudioClip timeoutSfx;
    public AudioClip retrySfx;
    public List<AudioClip> buttonSfxs;
    public AudioClip removeSfx;
    public AudioClip fallSfx;
    public List<AudioClip> starSfxs;
    public float sfxVolume = 0.8f;
    
    private void Awake()
    {
        if (gm != null && gm != this)
        {
            Destroy(gameObject);
            return;
        }
        gm = this;
        DontDestroyOnLoad(gameObject);
    }

    // Start is called before the first frame update
    void Start()
    {
        bgmAudioSource.volume = Mathf.Clamp01(bgmVolume);
        sfxAudioSource.volume = Mathf.Clamp01(sfxVolume);
        Initialize();   

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageWrite))
        {
            Permission.RequestUserPermission(Permission.ExternalStorageWrite);
        }
        if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageRead))
        {
            Permission.RequestUserPermission(Permission.ExternalStorageRead);
        }
#endif
    }

    // Update is called once per frame
    void Update()
    {
        // 입력 담당
        if (mm is null || !mm.IsReady) return;

        if (canPlay)
        {
            if ((Input.GetKeyUp(KeyCode.DownArrow) || Input.GetKeyUp(KeyCode.S)) && mm.gravityDownButton.interactable)
            {
                mm.ManipulateGravityDown();
            }
            else if ((Input.GetKeyUp(KeyCode.UpArrow) || Input.GetKeyUp(KeyCode.W)) && mm.gravityUpButton.interactable)
            {
                mm.ManipulateGravityUp();
            }
            else if ((Input.GetKeyUp(KeyCode.LeftArrow) || Input.GetKeyUp(KeyCode.A)) && mm.gravityLeftButton.interactable)
            {
                mm.ManipulateGravityLeft();
            }
            else if ((Input.GetKeyUp(KeyCode.RightArrow) || Input.GetKeyUp(KeyCode.D)) && mm.gravityRightButton.interactable)
            {
                mm.ManipulateGravityRight();
            }
            else if (Input.GetKeyUp(KeyCode.Space) && ((mm.gravityRetryButton.gameObject.activeInHierarchy
                && mm.gravityRetryButton.interactable) || (mm.gravityRetryHighlightedButton != null &&
                mm.gravityRetryHighlightedButton.gameObject.activeInHierarchy && mm.gravityRetryHighlightedButton.interactable)))
            {
                mm.Retry();
                if (SceneManager.GetActiveScene().name.Equals("Tutorial") && pm != null && pm.tutorialGuide != null)
                {
                    pm.tutorialGuide.RetryButtonDown();
                }
            }
            else if (Input.GetKeyUp(KeyCode.Return) && pm != null && pm.IsReady)
            {
                if (pm.nextButton.gameObject.activeInHierarchy && pm.nextButton.interactable)
                {
                    if (SceneManager.GetActiveScene().name.Equals("Tutorial"))
                    {
                        pm.TutorialNext();
                        PlayButtonSFX();
                    }
                    else
                    {
                        pm.PlayNext();
                        PlayButtonSFX();
                    }
                }
                else if (pm.resultUI.gameObject.activeInHierarchy)
                {
                    pm.Quit();
                    PlayButtonSFX();
                }
                else if (pm.quitHighlightedButton.gameObject.activeInHierarchy && pm.quitHighlightedButton.interactable)
                {
                    pm.Ending();
                    PlayButtonSFX();
                }
                else if (pm.messageUI.gameObject.activeInHierarchy && pm.messageUI.messageOKButton.interactable)
                {
                    pm.messageUI.messageOKButton.onClick.Invoke();
                }
            }
            else if (Input.GetKeyUp(KeyCode.Escape) && pm != null && pm.IsReady)
            {
                if (pm.pauseButton.gameObject.activeInHierarchy && pm.pauseButton.interactable)
                {
                    pm.Pause();
                    PlayButtonSFX();
                }
                else if (pm.messageUI.gameObject.activeInHierarchy && pm.messageUI.messageXButton.interactable)
                {
                    pm.messageUI.messageXButton.onClick.Invoke();
                }
            }
        }

        for (int i = 0; i < 3; i++)
        {
            if (bgmAudioSource.clip == bgms[i])
            {
                bgmAudioSource.volume = Mathf.Clamp01(bgmVolume * bgmVolumeForEach[i]);
                break;
            }
        }
    }

    void Initialize()
    {
        playingMapIndex = -1;

        // TODO: 씬 바뀔 때마다 적절한 레벨 선택하고 MapManager 찾아서 맵 로드해야 함
        if (SceneManager.GetActiveScene().name.Equals("Main"))
        {
            bool isTutorialDone = true;
            try
            {
                if (!File.Exists(Application.persistentDataPath + "/TutorialDone.txt"))
                {
                    LoadFirst();
                    isTutorialDone = false;
                    
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }

            if (isTutorialDone)
            {
                if (bgmAudioSource.clip != bgms[0])
                {
                    bgmAudioSource.Stop();
                    bgmAudioSource.clip = bgms[0];
                    bgmAudioSource.Play();
                }
                StartCoroutine(InitializeMain());
            }
        }
        else if (SceneManager.GetActiveScene().name.Equals("Editor"))
        {
            if (bgmAudioSource.clip != bgms[2])
            {
                bgmAudioSource.Stop();
                bgmAudioSource.clip = bgms[2];
                bgmAudioSource.Play();
            }
            StartCoroutine(InitializeEditor());
        }
        else if (SceneManager.GetActiveScene().name.Equals("Mode"))
        {
            if (bgmAudioSource.clip != bgms[0])
            {
                bgmAudioSource.Stop();
                bgmAudioSource.clip = bgms[0];
                bgmAudioSource.Play();
            }
            StartCoroutine(InitializeMode());
        }
        else if (SceneManager.GetActiveScene().name.Equals("AdventureLevel"))
        {
            if (bgmAudioSource.clip != bgms[0])
            {
                bgmAudioSource.Stop();
                bgmAudioSource.clip = bgms[0];
                bgmAudioSource.Play();
            }
            StartCoroutine(InitializeAdventureLevel());
        }
        else if (SceneManager.GetActiveScene().name.Equals("Tutorial"))
        {
            if (bgmAudioSource.clip != bgms[1])
            {
                bgmAudioSource.Stop();
                bgmAudioSource.clip = bgms[1];
                bgmAudioSource.Play();
            }
            StartCoroutine(InitializeTutorial());
        }
        else if (SceneManager.GetActiveScene().name.Equals("Adventure"))
        {
            if (bgmAudioSource.clip != bgms[1])
            {
                bgmAudioSource.Stop();
                bgmAudioSource.clip = bgms[1];
                bgmAudioSource.Play();
            }
            StartCoroutine(InitializeAdventure());
        }
        else if (SceneManager.GetActiveScene().name.Equals("Custom"))
        {
            if (bgmAudioSource.clip != bgms[0])
            {
                bgmAudioSource.Stop();
                bgmAudioSource.clip = bgms[0];
                bgmAudioSource.Play();
            }
            StartCoroutine(InitializeCustom());
        }
        else if (SceneManager.GetActiveScene().name.Equals("First"))
        {
            if (bgmAudioSource.clip != bgms[0])
            {
                bgmAudioSource.Stop();
                bgmAudioSource.clip = bgms[0];
                bgmAudioSource.Play();
            }
        }

        else if (SceneManager.GetActiveScene().name.Equals("Credit"))
        {
            if (bgmAudioSource.clip != bgms[0])
            {
                bgmAudioSource.Stop();
                bgmAudioSource.clip = bgms[0];
                bgmAudioSource.Play();
            }
        }
    }

    public void EditorChangeBGM(EditorManager.EditPhase editPhase)
    {
        if (editPhase != EditorManager.EditPhase.Test && bgmAudioSource.clip != bgms[2])
        {
            bgmAudioSource.Stop();
            bgmAudioSource.clip = bgms[2];
            bgmAudioSource.Play();
        }
        if (editPhase == EditorManager.EditPhase.Test && bgmAudioSource.clip != bgms[1])
        {
            bgmAudioSource.Stop();
            bgmAudioSource.clip = bgms[1];
            bgmAudioSource.Play();
        }
    }

    // 나중에 CustoChangeBGM 만들어야지 !!
    public void CustomChangeBGM(PlayManager.CustomPhase customPhase)
    {
        if (customPhase == PlayManager.CustomPhase.Open && bgmAudioSource.clip != bgms[0])
            {
            bgmAudioSource.Stop();
            bgmAudioSource.clip = bgms[0];
            bgmAudioSource.Play();
            }
        if (customPhase == PlayManager.CustomPhase.Ingame && bgmAudioSource.clip != bgms[1])
        {
            bgmAudioSource.Stop();
            bgmAudioSource.clip = bgms[1];
            bgmAudioSource.Play();
        }
    }

    public void PlayBallSFX()
    {
        int r = UnityEngine.Random.Range(0, ballSfxs.Count);
        sfxAudioSource.PlayOneShot(ballSfxs[r]);
    }

    public void PlayIronSFX(int moveDistance, float volumeScale = 1f)
    {
        if (moveDistance < 1 || moveDistance > 8) return;
        sfxAudioSource.PlayOneShot(ironSfxs[moveDistance - 1], Mathf.Clamp01(volumeScale * sfxVolume));
    }

    public void PlayWallSFX()
    {
        sfxAudioSource.PlayOneShot(wallSfx);
    }

    public void PlayShutterSFX()
    {
        sfxAudioSource.PlayOneShot(shutterSfx);
    }

    public void PlaySquashedSFX()
    {
        sfxAudioSource.PlayOneShot(squashedSfx);
    }

    public void PlayBurnedSFX()
    {
        sfxAudioSource.PlayOneShot(burnedSfx);
    }

    public void PlayEscapedSFX()
    {
        sfxAudioSource.PlayOneShot(escapedSfx);
    }

    public void PlayTimeoutSFX()
    {
        sfxAudioSource.PlayOneShot(timeoutSfx);
    }

    public void PlayRetrySFX()
    {
        sfxAudioSource.PlayOneShot(retrySfx);
    }

    public void PlayButtonSFX()
    {
        int r = UnityEngine.Random.Range(0, buttonSfxs.Count);
        sfxAudioSource.PlayOneShot(buttonSfxs[r]);
    }

    public void PlayRemoveSFX()
    {
        sfxAudioSource.PlayOneShot(removeSfx);
    }

    public void PlayFallSFX(float volume)
    {
        sfxAudioSource.PlayOneShot(fallSfx, Mathf.Clamp01(volume * sfxVolume));
    }

    public void PlayStarSFX(int num)
    {
        sfxAudioSource.PlayOneShot(starSfxs[num]);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void LoadEditor()
    {
        StartCoroutine(SceneLoading("Editor"));
    }

    public void LoadMain()
    {
        StartCoroutine(SceneLoading("Main"));
    }

    public void LoadMode()
    {
        StartCoroutine(SceneLoading("Mode"));
    }

    public void LoadAdventureLevel()
    {
        StartCoroutine(SceneLoading("AdventureLevel"));
    }

    public void LoadTutorial()
    {
        StartCoroutine(SceneLoading("Tutorial"));
    }

    public void LoadCustom()
    {
        StartCoroutine(SceneLoading("Custom"));
    }

    public void LoadAdventureEasy()
    {
        adventureLevel = AdventureLevel.Easy;
        StartCoroutine(SceneLoading("Adventure"));
    }

    public void LoadAdventureNormal()
    {
        adventureLevel = AdventureLevel.Normal;
        StartCoroutine(SceneLoading("Adventure"));
    }

    public void LoadAdventureHard()
    {
        adventureLevel = AdventureLevel.Hard;
        StartCoroutine(SceneLoading("Adventure"));
    }

    public void LoadAdventureInsane()
    {
        adventureLevel = AdventureLevel.Insane;
        StartCoroutine(SceneLoading("Adventure"));
    }

    public void LoadFirst()
    {
        StartCoroutine(SceneLoading("First"));
    }

    public void LoadCredit()
    {
        StartCoroutine(SceneLoading("Credit"));
    }

    IEnumerator SceneLoading(string sceneName)
    {
        if (mm != null)
        {
            mm.loadingPanel.SetActive(true);
        }
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        while (!operation.isDone)
        {
            yield return null;
        }
        Initialize();
    }

    IEnumerator InitializeMain()
    {
        while (mm == null)
        {
            mm = GameObject.FindGameObjectWithTag("MapManager").GetComponent<MapManager>();
            if (mm == null)
            {
                mm = GameObject.Find("MapManager").GetComponent<MapManager>();
            }
            yield return null;
        }
        List<WallInfo> walls = new List<WallInfo>();

        walls.Add(new WallInfo(WallInfo.Type.Vertical, 5, 3));
        walls.Add(new WallInfo(WallInfo.Type.Vertical, 6, 3));
        walls.Add(new WallInfo(WallInfo.Type.Vertical, 3, 2));
        walls.Add(new WallInfo(WallInfo.Type.Vertical, 4, 2));
        walls.Add(new WallInfo(WallInfo.Type.Vertical, 4, 1));
        walls.Add(new WallInfo(WallInfo.Type.Vertical, 5, 1));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 6));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 6, 6));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 6));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 1, 5));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 2, 5));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 3, 5));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 4, 5));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 5));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 6, 5));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 5));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 1, 4));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 2, 4));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 3, 4));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 4, 4));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 4));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 6, 4));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 4));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 1, 3));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 2, 3));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 3, 3));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 4, 3));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 3));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 3));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 1, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 2, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 3, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 4, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 1));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 6, 1));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 1));
        walls.Add(new WallInfo(WallInfo.Type.ExitVertical, 7, 4));

        List<ObjectInfo> objects = new List<ObjectInfo>();

        mm.afterGravity = MainAfterGravity;

        mm.Initialize(7, 7, walls, objects, "d", float.PositiveInfinity);
        mm.TimeActivate();
        canPlay = true;
    }

    IEnumerator InitializeEditor()
    {
        while (mm == null)
        {
            mm = GameObject.FindGameObjectWithTag("MapManager").GetComponent<MapManager>();
            if (mm == null)
            {
                mm = GameObject.Find("MapManager").GetComponent<MapManager>();
            }
            yield return null;
        }
        canPlay = false;
    }

    IEnumerator InitializeCustom()
    {
        while (mm == null)
        {
            mm = GameObject.FindGameObjectWithTag("MapManager").GetComponent<MapManager>();
            if (mm == null)
            {
                mm = GameObject.Find("MapManager").GetComponent<MapManager>();
            }
            yield return null;
        }
        canPlay = false;

        while (pm == null)
        {
            pm = GameObject.FindGameObjectWithTag("PlayManager").GetComponent<PlayManager>();
            if (pm != null) break;
            else
            {
                pm = GameObject.Find("PlayManager").GetComponent<PlayManager>();
            }
            yield return null;
        }

        pm.Initialize(PlayManager.Mode.Custom);

        mm.afterGravity = pm.PlayAfterGravity;
    }

    IEnumerator InitializeMode()
    {
        while (mm == null)
        {
            mm = GameObject.FindGameObjectWithTag("MapManager").GetComponent<MapManager>();
            if (mm == null)
            {
                mm = GameObject.Find("MapManager").GetComponent<MapManager>();
            }
            yield return null;
        }
        List<WallInfo> walls = new List<WallInfo>();

        walls.Add(new WallInfo(WallInfo.Type.Vertical, 1, 1));
        walls.Add(new WallInfo(WallInfo.Type.Vertical, 1, 4));
        walls.Add(new WallInfo(WallInfo.Type.Vertical, 1, 5));
        walls.Add(new WallInfo(WallInfo.Type.Vertical, 1, 7));
        walls.Add(new WallInfo(WallInfo.Type.Vertical, 2, 2));
        walls.Add(new WallInfo(WallInfo.Type.Vertical, 2, 5));
        walls.Add(new WallInfo(WallInfo.Type.Vertical, 3, 3));
        walls.Add(new WallInfo(WallInfo.Type.Vertical, 9, 2));  // TODO 나중에 해금
        // walls.Add(new WallInfo(WallInfo.Type.Vertical, 9, 4));  // TODO 나중에 해금
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 1, 8));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 2, 8));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 3, 8));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 4, 8));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 8));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 6, 8));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 8));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 8, 8));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 9, 8));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 10, 8));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 2, 7));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 3, 7));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 4, 7));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 7));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 6, 7));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 7));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 8, 7));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 9, 7));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 10, 7));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 2, 6));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 3, 6));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 4, 6));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 6));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 6, 6));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 6));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 8, 6));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 9, 6));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 10, 6));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 3, 5));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 4, 5));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 5));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 6, 5));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 5));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 8, 5));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 9, 5));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 10, 5));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 4, 4));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 4));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 6, 4));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 4));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 8, 4));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 9, 4));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 10, 4));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 3, 4));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 4, 3));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 3));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 6, 3));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 3));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 8, 3));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 9, 3));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 10, 3));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 1, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 2, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 4, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 6, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 8, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 9, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 10, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 2, 1));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 3, 1));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 4, 1));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 1));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 6, 1));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 1));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 8, 1));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 9, 1));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 10, 1));
        walls.Add(new WallInfo(WallInfo.Type.ExitVertical, 0, 3));

        // TODO 각 레벨에서 달성한 별 개수에 따라 생성
        if (File.Exists(Application.persistentDataPath + "/TutorialDone.txt")) {
            FileStream fs = new FileStream(Application.persistentDataPath + "/TutorialDone.txt", FileMode.Open);
            StreamReader sr = new StreamReader(fs, Encoding.UTF8);

            try
            {
                string line = sr.ReadLine();
                if (line.TrimEnd().Equals("3")) {
                    GameObject g = Instantiate(floorStarPrefab, new Vector3(), Quaternion.identity, mm.movableAndFixedGameObjects.transform);
                    g.transform.localPosition = new Vector3(7f, 6f, 0f);
                    GameObject h = Instantiate(floorStarPrefab, new Vector3(), Quaternion.identity, mm.movableAndFixedGameObjects.transform);
                    h.transform.localPosition = new Vector3(8f, 6f, 0f);
                    GameObject j = Instantiate(floorStarPrefab, new Vector3(), Quaternion.identity, mm.movableAndFixedGameObjects.transform);
                    j.transform.localPosition = new Vector3(9f, 6f, 0f);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
            finally
            {
                sr.Close();
                fs.Close();
            }

            
            // x좌표: 7f = 1개 이상, 6f = 2개 이상, 5f = 3개
            // y좌표: 8f = Easy, 6f = Normal, 4f = Hard, 2f = Insane
        }

        List<ObjectInfo> objects = new List<ObjectInfo>();

        mm.afterGravity = ModeAfterGravity;

        mm.Initialize(10, 9, walls, objects, "a", float.PositiveInfinity);
        mm.TimeActivate();
        canPlay = true;
    }

    IEnumerator InitializeAdventureLevel()
    {
        while (mm == null)
        {
            mm = GameObject.FindGameObjectWithTag("MapManager").GetComponent<MapManager>();
            if (mm == null)
            {
                mm = GameObject.Find("MapManager").GetComponent<MapManager>();
            }
            yield return null;
        }
        List<WallInfo> walls = new List<WallInfo>();

        walls.Add(new WallInfo(WallInfo.Type.Vertical, 1, 1));
        walls.Add(new WallInfo(WallInfo.Type.Vertical, 1, 4));
        walls.Add(new WallInfo(WallInfo.Type.Vertical, 1, 5));
        walls.Add(new WallInfo(WallInfo.Type.Vertical, 1, 7));
        walls.Add(new WallInfo(WallInfo.Type.Vertical, 2, 2));
        walls.Add(new WallInfo(WallInfo.Type.Vertical, 2, 5));
        walls.Add(new WallInfo(WallInfo.Type.Vertical, 3, 3));
        walls.Add(new WallInfo(WallInfo.Type.Vertical, 3, 9));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 1, 8));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 2, 8));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 3, 8));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 4, 8));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 8));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 6, 8));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 8));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 8, 8));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 2, 7));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 3, 7));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 4, 7));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 7));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 6, 7));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 7));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 8, 7));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 2, 6));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 3, 6));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 4, 6));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 6));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 6, 6));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 6));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 8, 6));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 3, 5));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 4, 5));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 5));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 6, 5));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 5));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 8, 5));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 3, 4));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 4, 4));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 4));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 6, 4));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 4));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 8, 4));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 4, 3));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 3));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 6, 3));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 3));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 8, 3));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 1, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 2, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 4, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 6, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 8, 2));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 2, 1));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 3, 1));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 4, 1));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 5, 1));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 6, 1));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 7, 1));
        walls.Add(new WallInfo(WallInfo.Type.Horizontal, 8, 1));
        walls.Add(new WallInfo(WallInfo.Type.ExitVertical, 0, 3));

        if (!File.Exists(Application.persistentDataPath + "/AdventureLevel.txt"))
        {
            try
            {
                FileStream fs = new FileStream(Application.persistentDataPath + "/AdventureLevel.txt", FileMode.Create);
                StreamWriter sw = new StreamWriter(fs, Encoding.UTF8);
                sw.WriteLine("0");
                sw.WriteLine("0");
                sw.WriteLine("0");
                sw.WriteLine("0");
                sw.Close();
                fs.Close();
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
        }
        else
        {
            FileStream fs = null;
            StreamReader sr = null;
            try
            {
                fs = new FileStream(Application.persistentDataPath + "/AdventureLevel.txt", FileMode.Open);
                using (sr = new StreamReader(fs, Encoding.UTF8))
                {
                    string line;
                    float endx = 7f;
                    float starty = 0f;
                    while ((line = sr.ReadLine()) != null) {
                        for(int i = 0; i < Convert.ToInt32(line); i++){
                            GameObject g = Instantiate(floorStarPrefab, new Vector3(), Quaternion.identity, mm.movableAndFixedGameObjects.transform);
                            g.transform.localPosition = new Vector3(endx, 8 - 2 * starty, 0f);
                            endx--;
                        }
                        endx = 7f;
                        starty++;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
            finally
            {
                sr.Close();
                fs.Close();
            }
        }

        List<ObjectInfo> objects = new List<ObjectInfo>();

        mm.afterGravity = AdventureLevelAfterGravity;

        mm.Initialize(8, 9, walls, objects, "a", float.PositiveInfinity);
        mm.TimeActivate();
        canPlay = true;
    }

    IEnumerator InitializeTutorial()
    {
        while (mm == null)
        {
            mm = GameObject.FindGameObjectWithTag("MapManager").GetComponent<MapManager>();
            if (mm == null)
            {
                mm = GameObject.Find("MapManager").GetComponent<MapManager>();
            }
            yield return null;
        }

        while (pm == null)
        {
            pm = GameObject.FindGameObjectWithTag("PlayManager").GetComponent<PlayManager>();
            if (pm != null) break;
            else
            {
                pm = GameObject.Find("PlayManager").GetComponent<PlayManager>();
            }
            yield return null;
        }

        pm.Initialize(PlayManager.Mode.Tutorial);
        mm.afterGravity = pm.TutorialAfterGravity;

        //mapList = Directory.GetFiles("Assets/PredefinedMaps/Tutorial/", "*.txt").ToList();

        playingMapIndex = -1;
        PlayNext();
    }
    
    IEnumerator InitializeAdventure()
    {
        while (mm == null)
        {
            mm = GameObject.FindGameObjectWithTag("MapManager").GetComponent<MapManager>();
            if (mm == null)
            {
                mm = GameObject.Find("MapManager").GetComponent<MapManager>();
            }
            yield return null;
        }

        while (pm == null)
        {
            pm = GameObject.FindGameObjectWithTag("PlayManager").GetComponent<PlayManager>();
            if (pm != null) break;
            else
            {
                pm = GameObject.Find("PlayManager").GetComponent<PlayManager>();
            }
            yield return null;
        }
        Debug.Log(adventureLevel);

        switch (adventureLevel)
        {
            case AdventureLevel.Easy:
                pm.Initialize(PlayManager.Mode.AdvEasy, true);
                break;
            case AdventureLevel.Normal:
                pm.Initialize(PlayManager.Mode.AdvNormal, true);
                break;
            case AdventureLevel.Hard:
                pm.Initialize(PlayManager.Mode.AdvHard, true);
                break;
            case AdventureLevel.Insane:
                pm.Initialize(PlayManager.Mode.AdvInsane, true);
                break;
            default:
                Debug.LogError("Play invalid: unknown adventure level");
                LoadAdventureLevel();
                yield break;
        }

        mm.afterGravity = pm.PlayAfterGravity;

        playingMapIndex = -1;
        PlayNext();
    }

    public void MainAfterGravity(MapManager.Flag flag)
    {
        switch (flag)
        {
            case MapManager.Flag.Escaped:
                LoadMode();
                break;
            case MapManager.Flag.MapEditor:
                LoadEditor();
                break;
            case MapManager.Flag.QuitGame:
                QuitGame();
                break;
            case MapManager.Flag.Credit:
                LoadCredit();
                break;
        }
    }

    public void ModeAfterGravity(MapManager.Flag flag)
    {
        switch (flag)
        {
            case MapManager.Flag.Escaped:
                LoadMain();
                break;
            case MapManager.Flag.Adventure:
                LoadAdventureLevel();
                break;
            case MapManager.Flag.Tutorial:
                LoadTutorial();
                break;
            case MapManager.Flag.Custom:
                LoadCustom();
                break;
            case MapManager.Flag.Training:
                // TODO
                break;
        }
    }

    public void AdventureLevelAfterGravity(MapManager.Flag flag)
    {
        switch (flag)
        {
            case MapManager.Flag.Escaped:
                LoadMode();
                break;
            case MapManager.Flag.AdvEasy:
                LoadAdventureEasy();
                break;
            case MapManager.Flag.AdvNormal:
                LoadAdventureNormal();
                break;
            case MapManager.Flag.AdvHard:
                LoadAdventureHard();
                break;
            case MapManager.Flag.AdvInsane:
                LoadAdventureInsane();
                break;
        }
    }

    public void TutorialNext()
    {
        if (pm == null || pm.MapFiles == null || pm.MapFiles.Count == 0) return;

        foreach (Transform obj in GameObject.Find("Objects").GetComponentsInChildren<Transform>())
        {
            if (obj.gameObject.name.Equals("Objects")) continue;
            Destroy(obj.gameObject);
        }

        PlayNext();
    }

    public void PlayNext()
    {
        // TODO mapList? or pm.mapFiles?
        if (pm == null || pm.MapFiles == null || pm.MapFiles.Count == 0) return;

        for (int i = playingMapIndex + 1; i <= pm.MapFiles.Count; i++)
        {
            if (pm.HasClearedAll)
            {
                // TODO Victory
                break;
            }
            MapManager.OpenFileFlag openFileFlag = mm.InitializeFromText(pm.MapFiles[i].text, out _, out _, out _, out _, out _, out _);
            if (openFileFlag != MapManager.OpenFileFlag.Success)
            {
                continue;
            }
            else
            {
                //Debug.Log("Map name: " + pm.MapFiles[i].name);
                playingMapIndex = i;
                pm.PlayAfterGravity(MapManager.Flag.Continued);
                mm.TimeActivate();
                canPlay = true;
                break;
            }
        }
    }

    public void ReviseStar(PlayManager.Mode mode, int star)
    {
        FileStream fs = null;
        StreamWriter sw = null;
        StreamReader sr = null;

        if (!File.Exists(Application.persistentDataPath + "/AdventureLevel.txt"))
        {
            try
            {
                fs = new FileStream(Application.persistentDataPath + "/AdventureLevel.txt", FileMode.Create);
                sw = new StreamWriter(fs, Encoding.UTF8);
                sw.WriteLine("0");
                sw.WriteLine("0");
                sw.WriteLine("0");
                sw.WriteLine("0");
                sw.Close();
                fs.Close();
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
        }

        fs = new FileStream(Application.persistentDataPath + "/AdventureLevel.txt", FileMode.Open, FileAccess.ReadWrite);
        using (sr = new StreamReader(fs, Encoding.UTF8))
        using (sw = new StreamWriter(fs, Encoding.UTF8))
        {
            string line;
            List<string> lines = new List<string>();
            while ((line = sr.ReadLine()) != null) {
                lines.Add(line.TrimEnd());
            }

            fs.Position = 0;
            int i = 11;
            foreach (string l in lines)
            {                
                if ((int) pm.PlayMode == i)
                {
                    sw.WriteLine(Math.Max(Convert.ToInt32(l), star));
                }
                else
                {
                    sw.WriteLine(Convert.ToInt32(l));
                }
                i++;
            }
        }
    }
}
