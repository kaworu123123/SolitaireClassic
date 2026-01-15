using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class TopInsetCanvasFitter : MonoBehaviour
{
    RectTransform rt;
    Canvas _rootCanvas;

    [SerializeField] bool forceRebuildLayout = true;
    [SerializeField] bool debugLog = false;

    [SerializeField] int extraTopPaddingPx = 0;      // �ǂ�����px�i��: ���@�����p�j
    [SerializeField] bool useCanvasScaleFactor = true;

    int _lastAppliedCanvasUnits = int.MinValue;      // ���ߓK�p�l�̋L�^
    int _lastInsetPx = -1;                           // ���߂�inset(px)
    float _lastScale = -1f;                          // ���߂�scaleFactor

    // �o�b�N�O���E���h����̗v���𗭂߂�iLateUpdate �Ń��C���X���b�h�ŏ�������j
    volatile int _pendingInsetPx = -1;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        _rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        // Awake �̓��C���X���b�h�Ȃ̂Œ��ړK�p���Ă�OK�B������ ApplyNow ���͈��S���ς݁B
        // ApplyNow(0); // ������
        ApplyNow(AdBannerController.LastTopInsetPx); // ★
    }

    void OnEnable()
    {
        AdBannerController.OnTopInsetChangedPx += ApplyNow;
    }
    void OnDisable()
    {
        AdBannerController.OnTopInsetChangedPx -= ApplyNow;
    }

    void LateUpdate()
    {
        // ���t���[���A�D��I�� pending �������i���ꂪ���C���X���b�h�j
        int latest = AdBannerController.LastTopInsetPx;
        // �ǂ��炩�V���������g���ipending �� -1 �Ȃ疳���j
        if (_pendingInsetPx >= 0)
        {
            // pending �����o���ăN���A�iatomic-ish�j
            int p = _pendingInsetPx;
            _pendingInsetPx = -1;
            latest = p;
        }
        ApplyNow(latest);
    }

    // �Ăяo���͂ǂ̃X���b�h����ł�OK�Ȃ悤�Ɉ��S������
    void ApplyNow(int insetPx)
    {
        // ��� pending �ɕۑ����Ă����i�Ăяo�����ʃX���b�h�ł������܂ŗ���j
        _pendingInsetPx = insetPx;

        // insetPx �����Ȃ� 0 ��
        if (insetPx < 0) insetPx = 0;

        // Canvas �� scaleFactor �����S�Ɏ擾����itry/catch �ŃK�[�h�j
        float scale = 1f;
        if (useCanvasScaleFactor)
        {
            if (_rootCanvas == null)
            {
                // �x���擾�i���C���X���b�h�Ȃ�m���Ɍ����邪�A�񃁃C���X���b�h�Ȃ� null �̂܂܁j
                _rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            }

            if (_rootCanvas != null)
            {
                try
                {
                    scale = Mathf.Max(0.0001f, _rootCanvas.scaleFactor);
                }
                catch (UnityEngine.UnityException ex)
                {
                    if (debugLog)
                        Debug.Log($"[TopInsetCanvasFitter] caught UnityException when reading scaleFactor: {ex.Message} (using fallback scale=1)");
                    // �񃁃C���X���b�h����̌Ăяo���Ȃǂŗ�O���o���ꍇ�̓t�H�[���o�b�N���ďI���B
                    // pending ���Z�b�g����Ă���̂� LateUpdate �ōĎ��s�����B
                    return;
                }
            }
        }

        // ���������� RectTransform �ɐG�邽�߁A����� try/catch �ŃK�[�h�B
        // �񃁃C���X���b�h�Ȃ炱���� UnityException ���o�� -> pending ������̂� LateUpdate �ōĎ��s�B
        try
        {
            int totalPx = Mathf.Max(0, insetPx + extraTopPaddingPx);
            int totalCanvasUnits = Mathf.RoundToInt(totalPx / scale);

            // ���ɓ����l��K�p�ς݂Ȃ牽�����Ȃ�
            if (totalCanvasUnits == _lastAppliedCanvasUnits && insetPx == _lastInsetPx) return;

            _lastInsetPx = insetPx;
            _lastScale = scale;
            _lastAppliedCanvasUnits = totalCanvasUnits;

            // Canvas �P�ʂɕϊ������I�t�Z�b�g�v�Z
            Vector2 offMin = rt.offsetMin;
            Vector2 offMax = rt.offsetMax;

            offMin.y = totalCanvasUnits;
            offMax.y = -totalCanvasUnits;

            rt.offsetMin = offMin;
            rt.offsetMax = offMax;

            if (forceRebuildLayout)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            if (debugLog)
                Debug.Log($"[TopInsetCanvasFitter] insetPx={insetPx}, scale={scale}, totalCanvas={totalCanvasUnits} (obj={name})");
        }
        catch (UnityEngine.UnityException ex)
        {
            // RectTransform ���ɃA�N�Z�X�����ۂɔ񃁃C���X���b�h�����O���o��P�[�X�͂�����B
            if (debugLog)
                Debug.Log($"[TopInsetCanvasFitter] caught UnityException while applying RectTransform: {ex.Message} (will retry on main thread)");
            // pending ���c���Ă���̂� LateUpdate �̎��t���[���ōĒ��킳���B
            return;
        }
    }

    [ContextMenu("Apply Last Immediately")]
    void ContextApply() => ApplyNow(AdBannerController.LastTopInsetPx);
}
