#import <UIKit/UIKit.h>

static NSUInteger CBHapticGeneration = 0;

static UIImpactFeedbackStyle CBResolveImpactStyle(int style)
{
    switch (style)
    {
        case 0:
            return UIImpactFeedbackStyleLight;
        case 2:
            return UIImpactFeedbackStyleHeavy;
        case 3:
            if (@available(iOS 13.0, *))
            {
                return UIImpactFeedbackStyleSoft;
            }
            return UIImpactFeedbackStyleLight;
        case 4:
            if (@available(iOS 13.0, *))
            {
                return UIImpactFeedbackStyleRigid;
            }
            return UIImpactFeedbackStyleHeavy;
        default:
            return UIImpactFeedbackStyleMedium;
    }
}

static void CBPlayImpact(int style, float intensity)
{
    UIImpactFeedbackGenerator *generator =
        [[UIImpactFeedbackGenerator alloc] initWithStyle:CBResolveImpactStyle(style)];
    [generator prepare];
    if (@available(iOS 13.0, *))
    {
        [generator impactOccurredWithIntensity:MAX(0.0f, MIN(1.0f, intensity))];
    }
    else
    {
        [generator impactOccurred];
    }
}

extern "C" void CBHapticsImpact(int style, float intensity, int accent)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        NSUInteger generation = ++CBHapticGeneration;
        CBPlayImpact(style, intensity);
        if (accent != 0)
        {
            dispatch_after(
                dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.055 * NSEC_PER_SEC)),
                dispatch_get_main_queue(),
                ^{
                    if (generation == CBHapticGeneration)
                    {
                        CBPlayImpact(0, 0.32f);
                    }
                });
        }
    });
}

extern "C" void CBHapticsCancel()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        ++CBHapticGeneration;
    });
}
