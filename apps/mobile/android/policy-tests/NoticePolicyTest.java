import com.example.alertario_mobile.NoticePolicy;

public class NoticePolicyTest {
    private static int checks;
    private static void check(boolean value) { checks++; if (!value) throw new AssertionError("Check " + checks); }
    public static void main(String[] args) {
        long now = 100000000;
        check(NoticePolicy.usable(false, "current", "accepted", "fresh", now, now - 60000, "2:1"));
        check(!NoticePolicy.usable(true, "current", "accepted", "fresh", now, now - 60000, "2:1"));
        check(!NoticePolicy.usable(false, "stale", "accepted", "fresh", now, now - 60000, "2:1"));
        check(!NoticePolicy.usable(false, "qualityReview", "accepted", "fresh", now, now - 60000, "2:1"));
        check(!NoticePolicy.usable(false, "current", "suspect", "fresh", now, now - 60000, "2:1"));
        check(!NoticePolicy.usable(false, "current", "accepted", "stale", now, now - 60000, "2:1"));
        check(!NoticePolicy.usable(false, "current", "accepted", "fresh", now, now - 3600001, "2:1"));
        check(!NoticePolicy.usable(false, "current", "accepted", "fresh", now, now - 3600000, "2:1"));
        check(!NoticePolicy.usable(false, "current", "accepted", "fresh", now, now + 1, "2:1"));
        check(!NoticePolicy.usable(false, "current", "accepted", "fresh", now, now - 60000, "unavailable"));
        check(!NoticePolicy.shouldNotify(false, "", 0, 0, "aboveAlertThreshold", now));
        check(NoticePolicy.shouldNotify(true, "noNotableChange", now - 2, now - 1, "aboveAlertThreshold", now));
        check(!NoticePolicy.shouldNotify(true, "aboveAlertThreshold", now - 2, now - 1, "aboveAlertThreshold", now));
        check(NoticePolicy.shouldNotify(true, "aboveAlertThreshold", now - 2, now - 1, "aboveEvacuationThreshold", now));
        check(!NoticePolicy.shouldNotify(true, "noNotableChange", now, now, "aboveAlertThreshold", now));
        check(!NoticePolicy.shouldNotify(true, "noNotableChange", now - 2, now, "aboveAlertThreshold", now - 1));
        check(!NoticePolicy.shouldNotify(true, "aboveAlertThreshold", now - 2, now - 1, "noNotableChange", now));
        check(!NoticePolicy.shouldNotify(true, "noNotableChange", now - 2, now - 1, "unknown", now));
        System.out.println(checks + " notification policy checks passed.");
    }
}
