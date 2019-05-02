#region Using Statements
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#endregion

namespace Delight
{
    public partial class Slider
    {
        /// <summary>
        /// Called when a property has been changed. 
        /// </summary>
        public override void OnPropertyChanged(object source, string property)
        {
            if (IgnoreObject)
                return;

            base.OnPropertyChanged(source, property);
            switch (property)
            {
                case nameof(Min):
                case nameof(Max):
                case nameof(Value):
                    SliderValueChanged();
                    break; 
                case nameof(IsReversed):
                    UpdateLayout();
                    break;
            }
        }

        /// <summary>
        /// Updates layout.
        /// </summary>
        public override bool UpdateLayout(bool notifyParent = true)
        {
            bool defaultDisableLayoutUpdate = DisableLayoutUpdate;
            DisableLayoutUpdate = true;

            if (Width == null) Width = Orientation == ElementOrientation.Horizontal ? Length : Breadth;
            if (Height == null) Height = Orientation == ElementOrientation.Horizontal ? Breadth : Length;

            // if vertical slider rotate slide region 90 degrees            
            if (Orientation == ElementOrientation.Vertical)
            {
                SliderRegion.DisableLayoutUpdate = true;
                SliderRegion.Width = new ElementSize(RectTransform.rect.height, ElementSizeUnit.Pixels);
                SliderRegion.Height = new ElementSize(RectTransform.rect.width, ElementSizeUnit.Pixels);
                SliderRegion.RectTransform.localRotation = Quaternion.Euler(new Vector3(0, 0, 90));
                SliderRegion.UpdateLayout(false);
                SliderRegion.DisableLayoutUpdate = false;
            }

            // update slider position
            UpdateSliderPosition(Value);

            DisableLayoutUpdate = defaultDisableLayoutUpdate;
            return base.UpdateLayout(notifyParent);
        }

        /// <summary>
        /// Called when the value of the slider changes (or any fields affecting the value).
        /// </summary>
        public virtual void SliderValueChanged()
        {
            // clamp value to min/max
            var clampedValue = Value.Clamp(Min, Max);
            ValueProperty.SetValue(this, Steps > 0 ? Nearest(clampedValue, Min, Max, Steps) : clampedValue, false);
            UpdateSliderPosition(Value);
        }

        /// <summary>
        /// Called on slider drag begin.
        /// </summary>
        public void SliderBeginDrag(DependencyObject sender, object eventArgs)
        {
            PointerEventData pointerData = eventArgs as PointerEventData;
            if (!CanSlide)
            {
                return;
            }

            SetSlideTo(pointerData);
        }

        /// <summary>
        /// Called on slider drag end.
        /// </summary>
        public void SliderEndDrag(DependencyObject sender, object eventArgs)
        {
            PointerEventData pointerData = eventArgs as PointerEventData;
            if (!CanSlide)
            {
                return;
            }

            SetSlideTo(pointerData, true);
        }

        /// <summary>
        /// Called on slider drag.
        /// </summary>
        public void SliderDrag(DependencyObject sender, object eventArgs)
        {
            PointerEventData pointerData = eventArgs as PointerEventData;
            if (!CanSlide)
            {
                return;
            }

            SetSlideTo(pointerData);
        }

        /// <summary>
        /// Called on potential drag begin (click).
        /// </summary>
        public void SliderInitializePotentialDrag(DependencyObject sender, object eventArgs)
        {
            PointerEventData pointerData = eventArgs as PointerEventData;
            if (!CanSlide)
            {
                return;
            }

            SetSlideTo(pointerData, true);
        }

        /// <summary>
        /// Sets slider value.
        /// </summary>
        public void SlideTo(float value)
        {
            float clampedValue = value.Clamp(Min, Max);
            Value = clampedValue;
        }

        /// <summary>
        /// Slides the slider to the given position.
        /// </summary>
        private void SetSlideTo(PointerEventData pointerData, bool isEndDrag = false)
        {
            var fillTransform = SliderFillRegion.RectTransform;

            Vector2 pos;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, pointerData.position, pointerData.pressEventCamera, out pos))
                return;

            // calculate slide percentage (transform.position.x/y is center of fill area)
            float p = 0;
            float slideAreaLength = fillTransform.rect.width - SliderHandleImageView.Width.Pixels;
            if (Orientation == ElementOrientation.Horizontal)
            {
                p = ((pos.x - fillTransform.localPosition.x + slideAreaLength / 2f) / slideAreaLength).Clamp(0, 1);
            }
            else
            {
                p = ((pos.y - fillTransform.localPosition.y + slideAreaLength / 2f) / slideAreaLength).Clamp(0, 1);
            }

            if (IsReversed)
            {
                // if we slide from left to right then the slide percentage is inverted
                p = 1 - p;
            }

            // set value
            float newValue = (Max - Min) * p + Min;
            newValue = Steps > 0 ? Nearest(newValue, Min, Max, Steps) : newValue;

            if (!SetValueOnDragEnded || (SetValueOnDragEnded && isEndDrag))
            {
                Value = newValue;
                ValueChanged?.Invoke(this, Value);
            }
            else
            {
                UpdateSliderPosition(newValue);
            }
        }

        /// <summary>
        /// Snaps to nearest value based on number of steps.
        /// </summary>
        public static float Nearest(float value, float min, float max, float steps)
        {
            var zerone = Mathf.Round((value - min) * steps / (max - min)) / steps;
            return zerone * (max - min) + min;
        }

        /// <summary>
        /// Sets slider position based on value.
        /// </summary>
        private void UpdateSliderPosition(float value)
        {
            float p = (value - Min) / (Max - Min);
            var fillTransform = SliderFillRegion.RectTransform;

            // set handle offset
            float fillWidth = fillTransform.rect.width;
            float slideAreaWidth = fillWidth - SliderHandleImageView.Width.Value;
            float sliderFillMargin = IsReversed ? SliderFillRegion.Margin.Right.Pixels : SliderFillRegion.Margin.Left.Pixels;
            float handleOffset = p * slideAreaWidth + sliderFillMargin;

            SliderHandleImageView.DisableLayoutUpdate = true;
            SliderHandleImageView.OffsetFromParent = IsReversed ?
                ElementMargin.FromRight(new ElementSize(handleOffset, ElementSizeUnit.Pixels)) :
                ElementMargin.FromLeft(new ElementSize(handleOffset, ElementSizeUnit.Pixels));
            SliderHandleImageView.UpdateLayout(false);
            SliderHandleImageView.DisableLayoutUpdate = false;

            // set fill percentage as to match the offset of the handle
            float fillP = (handleOffset + SliderHandleImageView.Width.Pixels / 2f) / fillWidth;
            SliderFillImageView.DisableLayoutUpdate = true;
            SliderFillImageView.Width = new ElementSize(fillP, ElementSizeUnit.Percents);
            SliderFillImageView.UpdateLayout(false);
            SliderFillImageView.DisableLayoutUpdate = false;
        }
    }
}
