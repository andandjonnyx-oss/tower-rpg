// App Tracking Transparency (ATT) のネイティブ層。
//
// AppTrackingTransparency.framework のリンクは
// Assets/Editor/IosPostProcessBuild.cs が weak link で追加する。
//
// 結果通知にコールバックを使わず、C# 側から状態をポーリングさせている。
// IL2CPP での関数ポインタマーシャリング（MonoPInvokeCallback）を避けるため。

#import <Foundation/Foundation.h>

#if __has_include(<AppTrackingTransparency/AppTrackingTransparency.h>)
#import <AppTrackingTransparency/AppTrackingTransparency.h>
#define TOWERRPG_HAS_ATT 1
#endif

extern "C" {

// 0 = NotDetermined / 1 = Restricted / 2 = Denied / 3 = Authorized
int _TowerRpgGetTrackingAuthorizationStatus(void)
{
#ifdef TOWERRPG_HAS_ATT
    if (@available(iOS 14, *)) {
        return (int)[ATTrackingManager trackingAuthorizationStatus];
    }
#endif
    // iOS 14 未満は ATT の概念が無く IDFA を取得できるため Authorized 扱い。
    return 3;
}

void _TowerRpgRequestTrackingAuthorization(void)
{
#ifdef TOWERRPG_HAS_ATT
    if (@available(iOS 14, *)) {
        [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:
            ^(ATTrackingManagerAuthorizationStatus status) {
                // 結果は C# が _TowerRpgGetTrackingAuthorizationStatus() で取得する。
            }];
    }
#endif
}

}
