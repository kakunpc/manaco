using nadena.dev.ndmf;
using com.kakunvr.manaco;
using com.kakunvr.manaco.Editor;

// NDMFへのプラグイン登録。
// GeneratingフェーズはModularAvatarの処理後、最終ビルド前に実行されます。
[assembly: ExportsPlugin(typeof(ManacoPlugin))]

namespace com.kakunvr.manaco
{
    public class ManacoPlugin : Plugin<ManacoPlugin>
    {
        public override string QualifiedName => "com.kakunvr.manaco";
        public override string DisplayName => "MANACO";

        protected override void Configure()
        {
            // ModularAvatarのTransformingフェーズ後に実行するためGeneratingを使用
            InPhase(BuildPhase.Generating)
                .AfterPlugin("nadena.dev.modular-avatar")
                .Run("Apply Custom Eye SubMesh", ctx => new ManacoPass().Execute(ctx))
                .PreviewingWith(new ManacoPreviewFilter());
        }
    }
}
