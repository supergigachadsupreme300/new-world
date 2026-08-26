using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public partial class CutsceneManager : MonoBehaviour
{
    private IEnumerator PanCamera(Vector3 startPos, Vector3 endPos, Vector3 lookTarget, float duration)
    {
        if (_mainCamera == null)
            yield break;
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, timer / duration);
            _mainCamera.transform.position = Vector3.Lerp(startPos, endPos, t);
            _mainCamera.transform.LookAt(lookTarget);
            yield return null;
        }
    }

    private IEnumerator PolicePatrol(Transform officer, Vector3 pointA, Vector3 pointB, float duration = 4f)
    {
        while (true)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                officer.position = Vector3.Lerp(pointA, pointB, Mathf.Min(t / duration, 1f));
                FaceMoveDirection(officer, pointB - pointA);
                yield return null;
            }
            t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                officer.position = Vector3.Lerp(pointB, pointA, Mathf.Min(t / duration, 1f));
                FaceMoveDirection(officer, pointA - pointB);
                yield return null;
            }
        }
    }

    private void FaceMoveDirection(Transform t, Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f)
            return;
        t.rotation = Quaternion.LookRotation(-dir.normalized);
    }

    private IEnumerator WalkStraight(Transform t, Vector3 from, Vector3 to, float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float p = Mathf.Min(timer / duration, 1f);
            t.position = Vector3.Lerp(from, to, p);
            FaceMoveDirection(t, to - from);
            yield return null;
        }
        t.position = to;
    }

    private IEnumerator WifeLookBack(Transform wife)
    {
        Quaternion startRot = wife.transform.rotation;
        Quaternion lookBack = Quaternion.Euler(0, 0, 0);
        float dur = 1.5f;
        float elapsed = 0f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            wife.transform.rotation = Quaternion.Slerp(startRot, lookBack, t);
            yield return null;
        }
        yield return new WaitForSeconds(3f);

        elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            wife.transform.rotation = Quaternion.Slerp(lookBack, startRot, t);
            yield return null;
        }
    }

    private Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildRecursive(root.GetChild(i), name);
            if (result != null)
                return result;
        }
        return null;
    }

    private IEnumerator IdleBob(Transform t, float amplitude)
    {
        if (t == null) yield break;
        Vector3 basePos = t.position;
        float phase = 0f;
        while (t != null)
        {
            phase += Time.deltaTime * 1.6f;
            t.position = basePos + Vector3.up * (Mathf.Sin(phase) * amplitude);
            yield return null;
        }
    }

    private IEnumerator ShowSubtitle(string textKey, float duration)
    {
        if (_canvas == null)
            _canvas = Object.FindAnyObjectByType<Canvas>();
        if (_canvas == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }
        DestroySubtitle();
        _subtitleGO = new GameObject("CutsceneSubtitle");
        _subtitleGO.transform.SetParent(_canvas.transform, false);
        var tmp = _subtitleGO.AddComponent<TextMeshProUGUI>();
        if (_uiManager != null && _uiManager.defaultTmpFont != null)
            tmp.font = _uiManager.defaultTmpFont;
        tmp.text = Localization.T(textKey);
        tmp.fontSize = 26;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        var rt = _subtitleGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 40f);
        rt.sizeDelta = new Vector2(900f, 60f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
                break;
            yield return null;
        }
        DestroySubtitle();
    }

    private void DestroySubtitle()
    {
        if (_subtitleGO != null)
        {
            Destroy(_subtitleGO);
            _subtitleGO = null;
        }
    }

    private IEnumerator WalkAnimation(GameObject model, float walkSpeed)
    {
        if (model == null) yield break;
        var hipL = model.transform.Find("HipL");
        var hipR = model.transform.Find("HipR");
        var shoulderL = model.transform.Find("ShoulderL");
        var shoulderR = model.transform.Find("ShoulderR");

        if (hipL == null && hipR == null && shoulderL == null && shoulderR == null) yield break;

        float freq = walkSpeed * 1.8f;
        float legAngle = 25f;
        float armAngle = 15f;

        while (model != null)
        {
            float theta = Time.time * freq;
            float sinVal = Mathf.Sin(theta);

            if (hipL != null) hipL.localRotation = Quaternion.Euler(sinVal * legAngle, 0f, 0f);
            if (hipR != null) hipR.localRotation = Quaternion.Euler(-sinVal * legAngle, 0f, 0f);
            if (shoulderL != null) shoulderL.localRotation = Quaternion.Euler(-sinVal * armAngle, 0f, 0f);
            if (shoulderR != null) shoulderR.localRotation = Quaternion.Euler(sinVal * armAngle, 0f, 0f);

            yield return null;
        }
    }

    private void StopWalkAnimation()
    {
        if (_walkAnimRoutine != null)
        {
            StopCoroutine(_walkAnimRoutine);
            _walkAnimRoutine = null;
        }
    }

    private void ResetLimbRotations(GameObject model)
    {
        if (model == null) return;
        foreach (string name in new[] { "HipL", "HipR", "ShoulderL", "ShoulderR" })
        {
            var t = model.transform.Find(name);
            if (t != null) t.localRotation = Quaternion.identity;
        }
    }
}
