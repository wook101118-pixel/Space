using System;
using System.Collections.Generic;
using UnityEngine;

namespace PortableEndingSystem
{
    [Serializable]
    public sealed class EndingCreditPhotoData
    {
        [SerializeField] private Sprite sprite;
        [SerializeField] private string anchorText = "GAME DIRECTOR";
        [Min(1)]
        [SerializeField] private int anchorOccurrence = 1;
        [SerializeField] private Vector2 displaySize = new Vector2(320f, 180f);
        [SerializeField] private float verticalOffset;

        public Sprite Sprite => sprite;
        public string AnchorText => anchorText;
        public int AnchorOccurrence => Mathf.Max(1, anchorOccurrence);
        public Vector2 DisplaySize => new Vector2(
            displaySize.x > 0f ? displaySize.x : 320f,
            displaySize.y > 0f ? displaySize.y : 180f);
        public float VerticalOffset => verticalOffset;
    }

    [CreateAssetMenu(
        fileName = "Ending Credits Data",
        menuName = "Portable Ending/Ending Credits Data")]
    public sealed class EndingCreditsData : ScriptableObject
    {
        public const string DefaultCreditsTemplate = @"THE END


모든 여정이 끝났습니다.


{GAME_TITLE}


A GAME BY

{DEVELOPER_NAME}


GAME DESIGN
게임 디자인

{DEVELOPER_NAME}


PROGRAMMING
프로그래밍

{DEVELOPER_NAME}


ART DIRECTION
아트 디렉션

{DEVELOPER_NAME}


SPECIAL THANKS
플레이해 주신 모든 분께 감사드립니다.


THANK YOU FOR PLAYING";

        [Header("Content")]
        [SerializeField] private string gameTitle = "몬타젬";
        [SerializeField] private string developerName = "개발자 이름";
        [TextArea(20, 80)]
        [SerializeField] private string creditsTemplate = DefaultCreditsTemplate;

        [Header("Credit Photos")]
        [SerializeField] private List<EndingCreditPhotoData> photos = new List<EndingCreditPhotoData>();
        [Min(1f)]
        [SerializeField] private float centerTextWidth = 760f;
        [Min(0f)]
        [SerializeField] private float photoGap = 70f;

        [Header("Developer Name Highlight")]
        [SerializeField] private Color nameHighlightColor = new Color(1f, 0.92f, 0.25f, 0.75f);
        [SerializeField] private Color highlightedNameTextColor = Color.black;

        [Header("Credits Timing")]
        [Min(0f)]
        [SerializeField] private float initialDelay = 2f;
        [Min(1f)]
        [SerializeField] private float scrollSpeed = 45f;
        [Min(0f)]
        [SerializeField] private float endHoldDuration = 2f;
        [Min(0f)]
        [SerializeField] private float endActionsFadeDuration = 1f;

        [Header("Audio")]
        [SerializeField] private AudioClip bgmClip;
        [Range(0f, 1f)]
        [SerializeField] private float bgmVolume = 0.7f;
        [SerializeField] private bool loopBgm = true;
        [Min(0f)]
        [SerializeField] private float bgmFadeInDuration = 2.5f;
        [Min(0f)]
        [SerializeField] private float bgmFadeOutDuration = 2.5f;

        [Header("Scene Transition")]
        [SerializeField] private string exitSceneName = "MainMenu";
        [SerializeField] private bool allowEscapeExit = true;
        [Min(0f)]
        [SerializeField] private float screenFadeInDuration = 2.5f;
        [Min(0f)]
        [SerializeField] private float screenFadeOutDuration = 2.5f;

        public string GameTitle => string.IsNullOrWhiteSpace(gameTitle) ? "몬타젬" : gameTitle.Trim();
        public string DeveloperName => string.IsNullOrWhiteSpace(developerName) ? "개발자 이름" : developerName.Trim();
        public string CreditsTemplate => string.IsNullOrEmpty(creditsTemplate) ? DefaultCreditsTemplate : creditsTemplate;
        public IReadOnlyList<EndingCreditPhotoData> Photos => photos;
        public float CenterTextWidth => Mathf.Max(1f, centerTextWidth);
        public float PhotoGap => Mathf.Max(0f, photoGap);
        public Color NameHighlightColor => nameHighlightColor;
        public Color HighlightedNameTextColor => highlightedNameTextColor;
        public float InitialDelay => Mathf.Max(0f, initialDelay);
        public float ScrollSpeed => Mathf.Max(1f, scrollSpeed);
        public float EndHoldDuration => Mathf.Max(0f, endHoldDuration);
        public float EndActionsFadeDuration => Mathf.Max(0f, endActionsFadeDuration);
        public AudioClip BgmClip => bgmClip;
        public float BgmVolume => Mathf.Clamp01(bgmVolume);
        public bool LoopBgm => loopBgm;
        public float BgmFadeInDuration => Mathf.Max(0f, bgmFadeInDuration);
        public float BgmFadeOutDuration => Mathf.Max(0f, bgmFadeOutDuration);
        public string ExitSceneName => string.IsNullOrWhiteSpace(exitSceneName) ? string.Empty : exitSceneName.Trim();
        public bool AllowEscapeExit => allowEscapeExit;
        public float ScreenFadeInDuration => Mathf.Max(0f, screenFadeInDuration);
        public float ScreenFadeOutDuration => Mathf.Max(0f, screenFadeOutDuration);

        private void OnValidate()
        {
            centerTextWidth = Mathf.Max(1f, centerTextWidth);
            photoGap = Mathf.Max(0f, photoGap);
            initialDelay = Mathf.Max(0f, initialDelay);
            scrollSpeed = Mathf.Max(1f, scrollSpeed);
            endHoldDuration = Mathf.Max(0f, endHoldDuration);
            endActionsFadeDuration = Mathf.Max(0f, endActionsFadeDuration);
            bgmVolume = Mathf.Clamp01(bgmVolume);
            bgmFadeInDuration = Mathf.Max(0f, bgmFadeInDuration);
            bgmFadeOutDuration = Mathf.Max(0f, bgmFadeOutDuration);
            screenFadeInDuration = Mathf.Max(0f, screenFadeInDuration);
            screenFadeOutDuration = Mathf.Max(0f, screenFadeOutDuration);
        }
    }
}
