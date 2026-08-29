#import <Foundation/Foundation.h>
#import <AppTrackingTransparency/AppTrackingTransparency.h>
#import <UserMessagingPlatform/UserMessagingPlatform.h>

extern "C" void UnitySendMessage(const char *obj, const char *method, const char *msg);

namespace
{
    NSString *sUnityGameObjectName = nil;
    BOOL sFlowInProgress = NO;
    BOOL sConsentInformationUpdated = NO;

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

    void SendPrivacyState(BOOL flowCompleted, BOOL canRequestAds, NSError *error)
    {
        UMPConsentInformation *consentInformation = UMPConsentInformation.sharedInstance;
        NSDictionary *payload = @{
            @"flowCompleted": @(flowCompleted),
            @"canRequestAds": @(canRequestAds),
            @"privacyOptionsRequired": @(
                consentInformation.privacyOptionsRequirementStatus
                    == UMPPrivacyOptionsRequirementStatusRequired),
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
        UnitySendMessage(
            sUnityGameObjectName.UTF8String,
            "OnIosPrivacyStateUpdated",
            json.UTF8String);
        sFlowInProgress = NO;
    }

    void ResolveAttThenPublish()
    {
        BOOL canRequestAds = UMPConsentInformation.sharedInstance.canRequestAds;
        if (!canRequestAds)
        {
            SendPrivacyState(YES, NO, nil);
            return;
        }

        if (@available(iOS 14, *))
        {
            if (ATTrackingManager.trackingAuthorizationStatus
                == ATTrackingManagerAuthorizationStatusNotDetermined)
            {
                [ATTrackingManager
                    requestTrackingAuthorizationWithCompletionHandler:
                        ^(ATTrackingManagerAuthorizationStatus status) {
                            dispatch_async(dispatch_get_main_queue(), ^{
                                SendPrivacyState(YES, canRequestAds, nil);
                            });
                        }];
                return;
            }
        }

        SendPrivacyState(YES, canRequestAds, nil);
    }

    void StartConsentUpdate()
    {
        UMPRequestParameters *parameters = [[UMPRequestParameters alloc] init];
        [UMPConsentInformation.sharedInstance
            requestConsentInfoUpdateWithParameters:parameters
                                 completionHandler:^(NSError *requestError) {
                                     dispatch_async(dispatch_get_main_queue(), ^{
                                         if (requestError != nil)
                                         {
                                             sConsentInformationUpdated = NO;
                                             SendPrivacyState(NO, NO, requestError);
                                             return;
                                         }

                                         sConsentInformationUpdated = YES;

                                         [UMPConsentForm
                                             loadAndPresentIfRequiredFromViewController:nil
                                                                      completionHandler:
                                                                          ^(NSError *formError) {
                                                                              dispatch_async(
                                                                                  dispatch_get_main_queue(), ^{
                                                                                      if (formError != nil)
                                                                                      {
                                                                                          SendPrivacyState(
                                                                                              NO,
                                                                                              NO,
                                                                                              formError);
                                                                                          return;
                                                                                      }

                                                                                      ResolveAttThenPublish();
                                                                                  });
                                                                          }];
                                     });
                                 }];
    }
}

extern "C" void ChromaBlastIosPrivacyRequestConsentUpdate(const char *unityGameObjectName)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (sFlowInProgress)
        {
            return;
        }

        sUnityGameObjectName = unityGameObjectName != nullptr
            ? [[NSString alloc] initWithUTF8String:unityGameObjectName]
            : nil;
        if (sUnityGameObjectName.length == 0)
        {
            return;
        }

        sFlowInProgress = YES;
        StartConsentUpdate();
    });
}

extern "C" void ChromaBlastIosPrivacyShowPrivacyOptions(const char *unityGameObjectName)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (sFlowInProgress)
        {
            return;
        }

        sUnityGameObjectName = unityGameObjectName != nullptr
            ? [[NSString alloc] initWithUTF8String:unityGameObjectName]
            : nil;
        if (sUnityGameObjectName.length == 0)
        {
            return;
        }

        if (!sConsentInformationUpdated)
        {
            sFlowInProgress = YES;
            StartConsentUpdate();
            return;
        }

        if (UMPConsentInformation.sharedInstance.privacyOptionsRequirementStatus
            != UMPPrivacyOptionsRequirementStatusRequired)
        {
            SendPrivacyState(
                YES,
                UMPConsentInformation.sharedInstance.canRequestAds,
                nil);
            return;
        }

        sFlowInProgress = YES;
        [UMPConsentForm
            presentPrivacyOptionsFormFromViewController:nil
                                      completionHandler:^(NSError *formError) {
                                          dispatch_async(dispatch_get_main_queue(), ^{
                                              if (formError != nil)
                                              {
                                                  SendPrivacyState(NO, NO, formError);
                                                  return;
                                              }

                                              ResolveAttThenPublish();
                                          });
                                      }];
    });
}
