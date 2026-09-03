#import <Foundation/Foundation.h>
#import <AppTrackingTransparency/AppTrackingTransparency.h>
#import <UserMessagingPlatform/UserMessagingPlatform.h>

extern "C" void UnitySendMessage(const char *obj, const char *method, const char *msg);

namespace
{
    NSString *sUnityGameObjectName = nil;
    BOOL sFlowInProgress = NO;
    BOOL sConsentInformationUpdated = NO;
    BOOL sPrivacyOptionsRequested = NO;

    void PresentPrivacyOptions();

    void SendPrivacyStage(NSString *stage)
    {
        if (sUnityGameObjectName.length == 0 || stage.length == 0)
        {
            return;
        }

        UnitySendMessage(
            sUnityGameObjectName.UTF8String,
            "OnIosPrivacyStageUpdated",
            stage.UTF8String);
    }

    NSString *SafeErrorMessage(NSError *error)
    {
        return error.localizedDescription ?: @"Unknown iOS privacy error.";
    }

    NSInteger CurrentAttStatus()
    {
        if (@available(iOS 14, *))
        {
            return (NSInteger)ATTrackingManager.trackingAuthorizationStatus;
        }

        return 4; // ATT unavailable on this OS version.
    }

    void SendPrivacyState(
        BOOL flowCompleted,
        BOOL canRequestAds,
        NSError *error,
        NSInteger privacyOptionsAction)
    {
        UMPConsentInformation *consentInformation = UMPConsentInformation.sharedInstance;
        NSDictionary *payload = @{
            @"flowCompleted": @(flowCompleted),
            @"canRequestAds": @(canRequestAds),
            @"privacyOptionsRequired": @(
                consentInformation.privacyOptionsRequirementStatus
                    == UMPPrivacyOptionsRequirementStatusRequired),
            @"consentStatus": @((NSInteger)consentInformation.consentStatus),
            @"privacyOptionsRequirementStatus": @(
                (NSInteger)consentInformation.privacyOptionsRequirementStatus),
            @"privacyOptionsAction": @(privacyOptionsAction),
            @"attAuthorizationStatus": @(CurrentAttStatus()),
            @"errorCode": @(error != nil ? error.code : 0),
            @"errorMessage": error != nil ? SafeErrorMessage(error) : @""
        };

        NSError *serializationError = nil;
        NSData *jsonData = [NSJSONSerialization dataWithJSONObject:payload
                                                           options:0
                                                             error:&serializationError];
        if (jsonData == nil || serializationError != nil || sUnityGameObjectName.length == 0)
        {
            sFlowInProgress = NO;
            return;
        }

        NSString *json = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        SendPrivacyStage(@"Final privacy state sent");
        UnitySendMessage(
            sUnityGameObjectName.UTF8String,
            "OnIosPrivacyStateUpdated",
            json.UTF8String);
        sFlowInProgress = NO;

        // A Settings tap can arrive while the launch consent form or ATT sheet is
        // active. Do not discard it: present the requested options after the
        // current native presentation has completed.
        if (sPrivacyOptionsRequested)
        {
            sPrivacyOptionsRequested = NO;
            dispatch_async(dispatch_get_main_queue(), ^{
                if (!sFlowInProgress)
                {
                    sFlowInProgress = YES;
                    PresentPrivacyOptions();
                }
            });
        }
    }

    void ResolveAttThenPublish(NSInteger privacyOptionsAction)
    {
        BOOL canRequestAds = UMPConsentInformation.sharedInstance.canRequestAds;
        if (!canRequestAds)
        {
            SendPrivacyStage(@"UMP resolved; ads unavailable");
            SendPrivacyState(YES, NO, nil, privacyOptionsAction);
            return;
        }

        if (@available(iOS 14, *))
        {
            if (ATTrackingManager.trackingAuthorizationStatus
                == ATTrackingManagerAuthorizationStatusNotDetermined)
            {
                SendPrivacyStage(@"ATT requested");
                [ATTrackingManager
                    requestTrackingAuthorizationWithCompletionHandler:
                        ^(ATTrackingManagerAuthorizationStatus status) {
                            dispatch_async(dispatch_get_main_queue(), ^{
                                SendPrivacyStage(@"ATT completed");
                                SendPrivacyState(YES, canRequestAds, nil, privacyOptionsAction);
                            });
                        }];
                return;
            }
        }

        SendPrivacyStage(@"ATT already resolved");
        SendPrivacyState(YES, canRequestAds, nil, privacyOptionsAction);
    }

    void StartConsentUpdate()
    {
        SendPrivacyStage(@"UMP update started");
        UMPRequestParameters *parameters = [[UMPRequestParameters alloc] init];
        [UMPConsentInformation.sharedInstance
            requestConsentInfoUpdateWithParameters:parameters
                                 completionHandler:^(NSError *requestError) {
                                     dispatch_async(dispatch_get_main_queue(), ^{
                                          if (requestError != nil)
                                          {
                                              SendPrivacyStage(@"UMP update failed");
                                              sConsentInformationUpdated = NO;
                                             // UMP may retain a valid consent result from an earlier
                                             // session even when the network update fails. Respect that
                                             // authoritative cached state instead of deadlocking ads.
                                              BOOL cachedCanRequestAds =
                                                  UMPConsentInformation.sharedInstance.canRequestAds;
                                              NSInteger action = sPrivacyOptionsRequested ? 3 : 0;
                                              sPrivacyOptionsRequested = NO;
                                              SendPrivacyState(
                                                  cachedCanRequestAds,
                                                  cachedCanRequestAds,
                                                  requestError,
                                                  action);
                                              return;
                                          }

                                          sConsentInformationUpdated = YES;
                                          SendPrivacyStage(@"UMP update completed");

                                          // This update was initiated by Privacy Options, so use
                                          // the dedicated UMP options API instead of silently
                                          // routing the tap through the launch-only form path.
                                          if (sPrivacyOptionsRequested)
                                          {
                                              sPrivacyOptionsRequested = NO;
                                              PresentPrivacyOptions();
                                              return;
                                          }

                                          SendPrivacyStage(@"UMP form requested");
                                          [UMPConsentForm
                                             loadAndPresentIfRequiredFromViewController:nil
                                                                      completionHandler:
                                                                          ^(NSError *formError) {
                                                                              dispatch_async(
                                                                                  dispatch_get_main_queue(), ^{
                                                                                      SendPrivacyStage(
                                                                                          formError == nil
                                                                                              ? @"UMP form completed"
                                                                                              : @"UMP form failed");
                                                                                      if (formError != nil)
                                                                                      {
                                                                                          BOOL cachedCanRequestAds =
                                                                                              UMPConsentInformation
                                                                                                  .sharedInstance
                                                                                                  .canRequestAds;
                                                                                          SendPrivacyState(
                                                                                              cachedCanRequestAds,
                                                                                              cachedCanRequestAds,
                                                                                              formError,
                                                                                              0);
                                                                                          return;
                                                                                      }

                                                                                      ResolveAttThenPublish(0);
                                                                                  });
                                                                          }];
                                     });
                                 }];
    }

    void PresentPrivacyOptions()
    {
        if (UMPConsentInformation.sharedInstance.privacyOptionsRequirementStatus
            != UMPPrivacyOptionsRequirementStatusRequired)
        {
            SendPrivacyState(
                YES,
                UMPConsentInformation.sharedInstance.canRequestAds,
                nil,
                1); // Privacy options are not required/available for this user.
            return;
        }

        SendPrivacyStage(@"Privacy Options form requested");
        [UMPConsentForm
            presentPrivacyOptionsFormFromViewController:nil
                                      completionHandler:^(NSError *formError) {
                                          dispatch_async(dispatch_get_main_queue(), ^{
                                              SendPrivacyStage(
                                                  formError == nil
                                                      ? @"Privacy Options completed"
                                                      : @"Privacy Options failed");
                                              if (formError != nil)
                                              {
                                                  BOOL cachedCanRequestAds =
                                                      UMPConsentInformation.sharedInstance.canRequestAds;
                                                  SendPrivacyState(
                                                      cachedCanRequestAds,
                                                      cachedCanRequestAds,
                                                      formError,
                                                      3);
                                                  return;
                                              }

                                              ResolveAttThenPublish(2);
                                          });
                                      }];
    }
}

extern "C" void ChromaBlastIosPrivacyRequestConsentUpdate(const char *unityGameObjectName)
{
    // Marshal-owned UTF-8 input is only guaranteed for the duration of this
    // native call. Copy it before dispatching work to the main queue.
    NSString *requestedUnityGameObjectName = unityGameObjectName != nullptr
        ? [NSString stringWithUTF8String:unityGameObjectName]
        : nil;
    dispatch_async(dispatch_get_main_queue(), ^{
        if (sFlowInProgress)
        {
            return;
        }

        sUnityGameObjectName = requestedUnityGameObjectName;
        if (sUnityGameObjectName.length == 0)
        {
            return;
        }

        sFlowInProgress = YES;
        SendPrivacyStage(@"Native bridge started");
        StartConsentUpdate();
    });
}

extern "C" void ChromaBlastIosPrivacyShowPrivacyOptions(const char *unityGameObjectName)
{
    NSString *requestedUnityGameObjectName = unityGameObjectName != nullptr
        ? [NSString stringWithUTF8String:unityGameObjectName]
        : nil;
    dispatch_async(dispatch_get_main_queue(), ^{
        sUnityGameObjectName = requestedUnityGameObjectName;
        if (sUnityGameObjectName.length == 0)
        {
            return;
        }

        SendPrivacyStage(@"Privacy Options bridge started");
        if (sFlowInProgress)
        {
            sPrivacyOptionsRequested = YES;
            return;
        }

        if (!sConsentInformationUpdated)
        {
            sPrivacyOptionsRequested = YES;
            sFlowInProgress = YES;
            StartConsentUpdate();
            return;
        }

        sFlowInProgress = YES;
        PresentPrivacyOptions();
    });
}
