using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PyExecutor.Settings;

internal static class PythonComponentInstanceRegistry
{
    private sealed class ControlBucket
    {
        private readonly List<WeakReference<HitokotoControl>> _controls = new();

        public void Add(HitokotoControl control)
        {
            lock (_controls)
            {
                Cleanup();
                _controls.Add(new WeakReference<HitokotoControl>(control));
            }
        }

        public IReadOnlyList<HitokotoControl> Snapshot()
        {
            lock (_controls)
            {
                Cleanup();
                if (_controls.Count == 0)
                {
                    return Array.Empty<HitokotoControl>();
                }

                var list = new List<HitokotoControl>(_controls.Count);
                foreach (var reference in _controls)
                {
                    if (reference.TryGetTarget(out var control))
                    {
                        list.Add(control);
                    }
                }

                return list;
            }
        }

        private void Cleanup()
        {
            _controls.RemoveAll(static reference => !reference.TryGetTarget(out _));
        }
    }

    private static readonly ConditionalWeakTable<PythonComponentSettings, ControlBucket> Buckets = new();

    public static void Register(HitokotoControl control)
    {
        if (control.Settings is null)
        {
            return;
        }

        var bucket = Buckets.GetValue(control.Settings, _ => new ControlBucket());
        bucket.Add(control);
    }

    public static IReadOnlyList<HitokotoControl> GetControls(PythonComponentSettings? settings)
    {
        if (settings is null)
        {
            return Array.Empty<HitokotoControl>();
        }

        if (!Buckets.TryGetValue(settings, out var bucket))
        {
            return Array.Empty<HitokotoControl>();
        }

        return bucket.Snapshot();
    }
}
