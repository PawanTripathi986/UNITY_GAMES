// Haptic feedback bridge for NexioCraft.Core.Haptics (C# [DllImport("__Internal")] nx_haptic).
#import <UIKit/UIKit.h>

extern "C" {

void nx_haptic(int kind)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        switch (kind) {
            case 0: {
                UISelectionFeedbackGenerator *generator = [[UISelectionFeedbackGenerator alloc] init];
                [generator selectionChanged];
                break;
            }
            case 1: {
                UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
                [generator impactOccurred];
                break;
            }
            case 2: {
                UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
                [generator impactOccurred];
                break;
            }
            case 3: {
                UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
                [generator impactOccurred];
                break;
            }
            case 4: {
                UINotificationFeedbackGenerator *generator = [[UINotificationFeedbackGenerator alloc] init];
                [generator notificationOccurred:UINotificationFeedbackTypeSuccess];
                break;
            }
            default: {
                UINotificationFeedbackGenerator *generator = [[UINotificationFeedbackGenerator alloc] init];
                [generator notificationOccurred:UINotificationFeedbackTypeWarning];
                break;
            }
        }
    });
}

}
