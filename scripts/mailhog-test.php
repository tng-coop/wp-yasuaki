#!/usr/bin/env php
<?php
// mailhog-test.php — send with wp_mail() and verify in MailHog

require '/var/www/html/wordpress/wp-load.php';

$mh_host   = getenv('MH_HOST')      ?: '127.0.0.1';
$mh_http   = intval(getenv('MH_HTTP_PORT') ?: '8025');
$mh_smtp   = intval(getenv('MH_SMTP_PORT') ?: '1025');
$mail_to   = getenv('MAIL_TO')      ?: 'inbox@example.test';
$mail_from = getenv('MAIL_FROM')    ?: 'wp-test@local.test';
$timeout   = intval(getenv('WAIT_SECS') ?: '20');
$mh_base   = "http://{$mh_host}:{$mh_http}";

// Force PHPMailer -> MailHog
add_action('phpmailer_init', function ($phpmailer) use ($mh_host, $mh_smtp) {
    $phpmailer->isSMTP();
    $phpmailer->Host = $mh_host;
    $phpmailer->Port = $mh_smtp;
    $phpmailer->SMTPAuth = false;
    $phpmailer->SMTPAutoTLS = false;
});

// Send
$subject = 'MailHog PHP test ' . time();
$bodyTxt = "Hello from PHP → MailHog at " . date('c');
$headers = [
    'From: wp-test <' . $mail_from . '>',
    'Date: ' . date(DATE_RFC2822),
];
$ok = wp_mail($mail_to, $subject, $bodyTxt, $headers);
if (!$ok) { fwrite(STDERR, "Mail failed ❌\n"); exit(1); }
echo "Mail sent ✅\nTo: $mail_to\nSubject: $subject\n";

// Poll for subject
$deadline = time() + $timeout;
$msg_id = null;
while (time() < $deadline) {
    $json = @file_get_contents("$mh_base/api/v2/messages");
    if ($json) {
        $data = json_decode($json, true);
        foreach (($data['items'] ?? []) as $item) {
            $subj = $item['Content']['Headers']['Subject'][0] ?? '';
            if ($subj === $subject) { $msg_id = $item['ID']; break 2; }
        }
    }
    usleep(300000); // 300ms
}
if (!$msg_id) { fwrite(STDERR, "Did not see message in MailHog within {$timeout}s.\n"); exit(1); }

// Fetch single (try v2 then v1)
$eid = rawurlencode($msg_id);
$msg_json = @file_get_contents("$mh_base/api/v2/messages/$eid");
if ($msg_json === false) { $msg_json = @file_get_contents("$mh_base/api/v1/messages/$eid"); }
if ($msg_json === false) { fwrite(STDERR, "Could not fetch message JSON.\n"); exit(1); }
$msg = json_decode($msg_json, true);

// Helpers to extract/decod e body
function first_header($msg, $name) {
    return $msg['Content']['Headers'][$name][0] ?? null;
}
function find_part($msg, $want = 'text/plain') {
    $parts = $msg['MIME']['Parts'] ?? [];
    // prefer wanted type, else html, else any text/*
    foreach ([$want, 'text/html'] as $type) {
        foreach ($parts as $p) {
            $ct = $p['Headers']['Content-Type'][0] ?? '';
            if (is_string($ct) && stripos($ct, $type) !== false) return $p;
        }
    }
    foreach ($parts as $p) {
        $ct = $p['Headers']['Content-Type'][0] ?? '';
        if (is_string($ct) && stripos($ct, 'text/') === 0) return $p;
    }
    return null;
}
function part_body_decoded($part) {
    if (!$part) return null;
    $enc = $part['Headers']['Content-Transfer-Encoding'][0] ?? '7bit';
    $raw = $part['Body'] ?? ($part['Content'] ?? '');
    if (!is_string($raw)) $raw = '';
    $enc = strtolower($enc);
    if ($enc === 'base64') {
        $dec = base64_decode($raw, true);
        return $dec !== false ? $dec : $raw;
    }
    if ($enc === 'quoted-printable') {
        return quoted_printable_decode($raw);
    }
    return $raw; // 7bit/8bit/binary
}
// Try parts first, then fallback to Content.Body
$part = find_part($msg, 'text/plain');
$body = part_body_decoded($part);
if ($body === null || $body === '') {
    $body = $msg['Content']['Body'] ?? '';
}

$from = first_header($msg, 'From') ?? '(none)';
$to   = first_header($msg, 'To')   ?? '(none)';
$date = first_header($msg, 'Date') ?? '(none)';

echo "=== MailHog confirmed ===\n";
echo "ID:   $msg_id\n";
echo "From: $from\n";
echo "To:   $to\n";
echo "Date: $date\n";
echo "Body:\n";
echo ($body !== '' ? $body : "(no body)") . "\n";
