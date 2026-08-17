using UnityEngine;

namespace SpaceGame.CommonUI.Tutorial
{
    [CreateAssetMenu(
        fileName = "TutorialPage",
        menuName = "Space/Common UI/Tutorial Page")]
    public sealed class TutorialPageData : ScriptableObject
    {
        [SerializeField] private Sprite image;
        [SerializeField] private string visualHint;
        [SerializeField] private string title;
        [SerializeField, TextArea(3, 10)] private string body;

        public Sprite Image => image;
        public string VisualHint => visualHint;
        public string Title => title;
        public string Body => body;

        public void Configure(
            Sprite pageImage,
            string pageTitle,
            string pageBody,
            string pageVisualHint = "")
        {
            image = pageImage;
            visualHint = pageVisualHint;
            title = pageTitle;
            body = pageBody;
        }
    }
}
