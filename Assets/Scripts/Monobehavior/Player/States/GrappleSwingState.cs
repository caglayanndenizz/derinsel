using UnityEngine;

public class GrappleSwingState : PlayerState
{
    Vector2     _anchorPoint;
    float       _ropeLength;
    float       _angle;           // radians from downward vertical (0 = directly below anchor)
    float       _angularVelocity; // rad/s, positive = clockwise
    float       _swingTimer;
    GrappleBolt _bolt;

    const float Gravity        = 14f;   // tunable gravity feel
    const float MousePushForce = 1.8f;  // how hard the player can push with mouse
    const float SwingDuration  = 2f;    // seconds before decay starts
    const float DecayRate      = 3.5f;  // angular velocity lost per second during decay
    const float StopThreshold  = 0.04f; // release when angular vel drops below this
    const float MaxRopeLength  = 14f;
    const float MinRopeLength  = 0.8f;

    public GrappleSwingState(Vector2 anchorPoint, float ropeLength, GrappleBolt bolt)
    {
        _anchorPoint = anchorPoint;
        _ropeLength  = Mathf.Clamp(ropeLength, MinRopeLength, MaxRopeLength);
        _bolt        = bolt;
    }

    public override void Enter(IPlayerContext context)
    {
        // Initial angle from player position relative to anchor
        Vector2 toPlayer = context.Rb.position - _anchorPoint;
        _angle = Mathf.Atan2(toPlayer.x, -toPlayer.y);

        // Convert current linear velocity to angular velocity
        Vector2 tangent = new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle));
        _angularVelocity = Vector2.Dot(context.Rb.linearVelocity, tangent) / _ropeLength;

        // Physics takeover: stop movement and go kinematic
        context.Rb.linearVelocity = Vector2.zero;
        context.Rb.bodyType = RigidbodyType2D.Kinematic;

        _swingTimer = 0f;
    }

    public override void Handle(IPlayerContext context)
    {
        float dt = Time.deltaTime;
        _swingTimer += dt;

        // ── Pendulum gravity ─────────────────────────────────────────────
        _angularVelocity += -Gravity * Mathf.Sin(_angle) / _ropeLength * dt;

        // ── Mouse push (active phase only) ───────────────────────────────
        if (_swingTimer < SwingDuration)
        {
            Vector2 mouseWorld = context.GetLongbowAimWorldPointAtCurrentMouse();
            Vector2 toMouse    = (mouseWorld - context.Rb.position).normalized;
            Vector2 tangent    = new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle));
            _angularVelocity  += Vector2.Dot(toMouse, tangent) * MousePushForce * dt;
        }
        else
        {
            // ── Momentum decay ───────────────────────────────────────────
            _angularVelocity = Mathf.MoveTowards(_angularVelocity, 0f, DecayRate * dt);
            if (Mathf.Abs(_angularVelocity) < StopThreshold)
            {
                Release(context);
                return;
            }
        }

        // Prevent full rotation over the anchor point
        _angle = Mathf.Clamp(_angle + _angularVelocity * dt, -Mathf.PI * 0.88f, Mathf.PI * 0.88f);

        // Update player position along pendulum arc
        Vector2 newPos = _anchorPoint + new Vector2(Mathf.Sin(_angle), -Mathf.Cos(_angle)) * _ropeLength;
        context.Rb.transform.position = new Vector3(newPos.x, newPos.y, context.Rb.transform.position.z);

        // Flip sprite to match swing direction
        float velX = Mathf.Cos(_angle) * _angularVelocity * _ropeLength;
        if (Mathf.Abs(velX) > 0.1f)
        {
            Vector3 scale = context.Rb.transform.localScale;
            scale.x = velX > 0f ? 1f : -1f;
            context.Rb.transform.localScale = scale;
        }
    }

    public override void Exit(IPlayerContext context)
    {
        // Restore physics
        context.Rb.bodyType = RigidbodyType2D.Dynamic;

        // Exit velocity matches swing momentum so movement feels continuous
        Vector2 tangent = new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle));
        context.Rb.linearVelocity = tangent * (_angularVelocity * _ropeLength);

        _bolt?.Detach();
    }

    void Release(IPlayerContext context)
    {
        context.SetState(new IdleState());  // triggers Exit → Detach
    }
}
