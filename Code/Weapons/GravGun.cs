public partial class GravGun : BaseCarriable
{
    public struct GrabState
    {
        public GameObject GameObject { get; set; }
        public Vector3 LocalOffset { get; set; }
        public Vector3 LocalNormal { get; set; }
        public Transform GrabOffset { get; set; }

        public Vector3 EndPoint
        {
            get
            {
                if ( !GameObject.IsValid() ) return LocalOffset;
                return GameObject.WorldTransform.PointToWorld( LocalOffset );
            }
        }

        public Vector3 EndNormal
        {
            get
            {
                if ( !GameObject.IsValid() ) return LocalNormal;
                return GameObject.WorldTransform.NormalToWorld( LocalNormal );
            }
        }

        public bool IsValid() => GameObject.IsValid();
        public Rigidbody Body => GameObject?.GetComponent<Rigidbody>();
    }

    [Sync] public GrabState _state { get; set; } = default;
    bool _preventReselect = false;

    public override void OnControl( Player player )
    {
        base.OnControl( player );

        if ( Scene.TimeScale == 0 )
            return;

        if ( _state.IsValid() )
        {
            if ( !Input.Down( "attack2" ) )
            {
                _state = default;
                _preventReselect = true;
                return;
            }

            if ( Input.Pressed( "attack1" ) )
            {
                _state = default;
                _preventReselect = true;
                return;
            }

            return;
        }

        if ( _preventReselect )
        {
            if ( !Input.Down( "attack2" ) )
                _preventReselect = false;
            return;
        }

        if ( Input.Down( "attack2" ) )
        {
            if ( FindGrabbedBody( out GrabState sh, player.EyeTransform ) )
            {
                _state = sh;
            }
        }
        else
        {
            _preventReselect = false;
        }
    }

    bool FindGrabbedBody( out GrabState state, Transform aim )
    {
        state = default;

        var tr = Scene.Trace.Ray( aim.Position, aim.Position + aim.Forward * 1000 )
                .IgnoreGameObjectHierarchy( GameObject.Root )
                .Run();

        if ( !tr.Hit || tr.Body is null ) return false;
        if ( tr.Component is not Rigidbody ) return false;

        var go = tr.Body.GameObject;
        if ( !go.IsValid() ) return false;

        var bodyTransform = tr.Body.Transform.WithScale( go.WorldScale );

        state.GameObject = go;
        state.LocalOffset = bodyTransform.PointToLocal( tr.HitPosition );
        state.LocalNormal = bodyTransform.NormalToLocal( tr.Normal );
        state.GrabOffset = aim.ToLocal( bodyTransform );

        return true;
    }
}
