#!/usr/bin/env php
<?php
// mailhog-test.php — send with wp_mail() and verify in MailHog

// --- WordPress bootstrap (fixed path) ---
require '/var/www/html/wordpress/wp-load.php';

// --- Config (override with env) ---
$mh_host   = getenv('MH_HOST')      ?: '127.0.0.1';
$mh_http   = intval(getenv('MH_HTTP_PORT') ?: '8025');
$mh_smtp   = intval(getenv('MH_SMTP_PORT') ?: '1025');
$mail_to   = getenv('MAIL_TO')      ?: 'inbox@example.test';
$mail_from = getenv('MAIL_FROM')    ?: 'wp-test@local.test';
$timeout   = intval(getenv('WAIT_SECS') ?: '20');
$mh_base   = "http://{$mh_host}:{$mh_http}";

// --- Force PHPMailer to MailHog ---
add_action('phpmailer_init', function ($phpmailer) use ($mh_host, $mh_smtp) {
    $phpmailer->isSMTP();
    $phpmailer->Host = $mh_host;
    $phpmailer->Port = $mh_smtp;
    $phpmailer->SMTPAuth = false;
    $phpmailer->SMTPAutoTLS = false;
});

// --- Send message ---
$subject = 'MailHog PHP test ' . time();
$body    = "Hello from PHP → MailHog at " . date('c');
$headers = [
    'From: wp-test <' . $mail_from . '>',
    'Date: ' . date(DATE_RFC2822),
];

$ok = wp_mail($mail_to, $subject, $body, $headers);

if (!$ok) {
    fwrite(STDERR, "Mail failed ❌\n");
    exit(1);
}
echo "Mail sent ✅\nTo: $mail_to\nSubject: $subject\n";

// --- Verify in MailHog ---
$deadline = time() + $timeout;
$msg_id = null;
while (time() < $deadline) {
    $json = @file_get_contents("$mh_base/api/v2/messages");
    if ($json) {
        $data = json_decode($json, true);
        foreach ($data['items'] as $item) {
            $subj = $item['Content']['Headers']['Subject'][0] ?? '';
            if ($subj === $subject) {
                $msg_id = $item['ID'];
                break 2;
            }
        }
    }
    sleep(1);
}

if (!$msg_id) {
    fwrite(STDERR, "Did not see message in MailHog within {$timeout}s.\n");
    exit(1);
}

// Fetch full message
$msg_json = @file_get_contents("$mh_base/api/v1/messages/" . urlencode($msg_id));
if ($msg_json) {
    $msg = json_decode($msg_json, true);
    $from = $msg['Content']['Headers']['From'][0] ?? '(none)';
    $to   = $msg['Content']['Headers']['To'][0] ?? '(none)';
    $date = $msg['Content']['Headers']['Date'][0] ?? '(none)';
    $body = $msg['MIME']['Parts'][0]['Body'] ?? '(no body)';

    echo "=== MailHog confirmed ===\n";
    echo "ID:   $msg_id\n";
    echo "From: $from\n";
    echo "To:   $to\n";
    echo "Date: $date\n";
    echo "Body:\n$body\n";
}
