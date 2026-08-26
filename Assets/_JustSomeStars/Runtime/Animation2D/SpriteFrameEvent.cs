using System;

namespace JustSomeStars.Runtime.Animation2D
{
    public enum SpriteFrameEventKind
    {
        FootContact = 0,
        ToolAttach = 1,
        ToolDetach = 2,
        Interaction = 3,
        Audio = 4,
        Vfx = 5,
    }

    [Serializable]
    public struct SpriteFrameEvent
    {
        [UnityEngine.SerializeField] private int frameIndex;
        [UnityEngine.SerializeField] private SpriteFrameEventKind kind;
        [UnityEngine.SerializeField] private string id;

        public SpriteFrameEvent(
            int frameIndex,
            SpriteFrameEventKind kind,
            string id)
        {
            this.frameIndex = frameIndex;
            this.kind = kind;
            this.id = id;
        }

        public int FrameIndex => frameIndex;
        public SpriteFrameEventKind Kind => kind;
        public string Id => id;
    }

    [Serializable]
    public struct SpriteFrameContact
    {
        [UnityEngine.SerializeField] private int frameIndex;
        [UnityEngine.SerializeField] private string id;

        public SpriteFrameContact(int frameIndex, string id)
        {
            this.frameIndex = frameIndex;
            this.id = id;
        }

        public int FrameIndex => frameIndex;
        public string Id => id;
    }
}
