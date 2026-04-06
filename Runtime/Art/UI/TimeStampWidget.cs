using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConceptFactory.Weather.UI
{
    /// <summary>
    /// Widget reutilizável de carimbo de tempo (UI Toolkit). Carrega
    /// <see cref="UxmlResourcesPath"/> de uma pasta <c>Resources</c> no pacote Weather.
    /// </summary>
    public sealed class TimeStampWidget : VisualElement
    {
        /// <summary>
        /// Caminho relativo a qualquer pasta <c>Resources</c> (sem extensão), ex.: <c>Widgets/TimeStampWidget</c>.
        /// </summary>
        public const string UxmlResourcesPath = "Widgets/TimeStampWidget";

        private const string UssClassRoot = "timestamp-widget";

        private Label _timeLabel;
        private VisualElement _icon;
        private VisualElement _rewind;
        private VisualElement _fastForward;

        private bool _transportVisible;
        private bool _iconVisible = true;

        /// <summary>Texto mostrado no relógio (ex. <c>14:30</c>).</summary>
        public string TimeText
        {
            get => _timeLabel != null ? _timeLabel.text : string.Empty;
            set
            {
                if (_timeLabel != null)
                    _timeLabel.text = value ?? string.Empty;
            }
        }

        /// <summary>Mostrar ou ocultar os controlos rewind / fast-forward.</summary>
        public bool TransportControlsVisible
        {
            get => _transportVisible;
            set
            {
                _transportVisible = value;
                ApplyTransportVisibility();
            }
        }

        /// <summary>Mostrar ou ocultar o ícone ao lado do texto.</summary>
        public bool IconVisible
        {
            get => _iconVisible;
            set
            {
                _iconVisible = value;
                ApplyIconVisibility();
            }
        }

        /// <summary>Clico no botão rewind (sprite invertido no UXML/USS).</summary>
        public event Action RewindClicked;

        /// <summary>Clico no botão fast-forward.</summary>
        public event Action FastForwardClicked;

        public TimeStampWidget()
        {
            AddToClassList(UssClassRoot);

            var tree = Resources.Load<VisualTreeAsset>(UxmlResourcesPath);
            if (tree == null)
            {
                Debug.LogError($"[TimeStampWidget] VisualTreeAsset em falta: Resources/{UxmlResourcesPath}.");
                return;
            }

            tree.CloneTree(this);

            _timeLabel = this.Q<Label>("TimeStampLabel");
            _icon = this.Q<VisualElement>("Icon");
            _rewind = this.Q<VisualElement>("Rewind");
            _fastForward = this.Q<VisualElement>("FF");

            if (_timeLabel == null)
                Debug.LogError("[TimeStampWidget] O UXML deve conter um Label com name=\"TimeStampLabel\".");

            if (_rewind != null)
                _rewind.RegisterCallback<ClickEvent>(OnRewindClick);
            if (_fastForward != null)
                _fastForward.RegisterCallback<ClickEvent>(OnFastForwardClick);

            ApplyTransportVisibility();
            ApplyIconVisibility();
        }

        private void OnRewindClick(ClickEvent e)
        {
            if (!_transportVisible)
                return;
            RewindClicked?.Invoke();
            e.StopPropagation();
        }

        private void OnFastForwardClick(ClickEvent e)
        {
            if (!_transportVisible)
                return;
            FastForwardClicked?.Invoke();
            e.StopPropagation();
        }

        private void ApplyTransportVisibility()
        {
            if (_rewind == null || _fastForward == null)
                return;

            var v = _transportVisible ? Visibility.Visible : Visibility.Hidden;
            _rewind.style.visibility = v;
            _fastForward.style.visibility = v;
        }

        private void ApplyIconVisibility()
        {
            if (_icon == null)
                return;

            _icon.style.display = _iconVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Substitui o fundo do elemento <c>Icon</c> (ex. <c>Ico_Sun</c> / <c>Ico_Moon</c> de Timestamp.png).</summary>
        public void SetIconBackgroundSprite(Sprite sprite)
        {
            if (_icon == null || sprite == null)
                return;

            _icon.style.backgroundImage = new StyleBackground(sprite);
        }

        public new class UxmlFactory : UxmlFactory<TimeStampWidget, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlStringAttributeDescription _timeText =
                new UxmlStringAttributeDescription { name = "time-text", defaultValue = "00:00" };

            private readonly UxmlBoolAttributeDescription _showTransport =
                new UxmlBoolAttributeDescription { name = "show-transport", defaultValue = false };

            private readonly UxmlBoolAttributeDescription _showIcon =
                new UxmlBoolAttributeDescription { name = "show-icon", defaultValue = true };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var w = (TimeStampWidget)ve;
                w.TimeText = _timeText.GetValueFromBag(bag, cc);
                w.TransportControlsVisible = _showTransport.GetValueFromBag(bag, cc);
                w.IconVisible = _showIcon.GetValueFromBag(bag, cc);
            }
        }
    }
}
