namespace AgentFrameworkWorkflows;

internal static class SampleEmails
{
    internal static readonly (string Title, string Email)[] Examples =
    [
        (
            "Example 1 - Escalate to human (negative + high urgency)",
            """
            From: Noam Cohen <noam.cohen@gmail.com>
            Subject: URGENT: Charged twice - fix today (Order #A1B2C3)

            Hi Support,

            I'm really upset. I was charged twice for order #A1B2C3 and this is urgent.
            Please fix it today. My phone is +972 52-123-4567.

            Thanks,
            Noam
            """
        ),
        (
            "Example 2 - Clarification needed (missing details)",
            """
            From: Yael Levi <yael.levi@gmail.com>
            Subject: Refund request - duplicate charge

            Hi Support,

            I think I was charged twice yesterday, but I'm not sure which order it was.
            Can you help and tell me what info you need?

            Thanks,
            Yael
            """
        ),
        (
            "Example 3 - Refund request created (enough details)",
            """
            From: Eitan Mizrahi <eitan.mizrahi@gmail.com>
            Subject: Duplicate charge for Order #Z9Y8X7 (details included)

            Hi,

            I noticed a duplicate charge, but it's not urgent.
            Order #Z9Y8X7
            Amounts: ₪499.90 charged twice
            Date: 2025-12-14
            Card: last 4 digits 1234
            Transaction IDs: TX-771122 and TX-771123

            Please review and refund the duplicate charge when you can.

            תודה,
            Eitan
            """
        ),
        (
            "Example 4 - Normal reply (not a refund)",
            """
            From: Dana Barak <dana.barak@gmail.com>
            Subject: Where is my shipment? (Order #H4J5K6)

            Hi Support,

            Can you please tell me the shipping status for order #H4J5K6?
            It's been a few days and I'm not sure if it shipped.

            Thanks,
            Dana
            """
        ),
    ];
}
