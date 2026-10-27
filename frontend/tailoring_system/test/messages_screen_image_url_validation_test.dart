import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/screens/messages_screen.dart';

void main() {
  test('accepts official Local WhatsApp campaign image references', () {
    expect(isValidCampaignImageUrl('campaign-media/offer.png'), isTrue);
    expect(isValidCampaignImageUrl('/campaign-media/offer.jpg'), isTrue);
    expect(isValidCampaignImageUrl('campaign-media/offer.webp'), isTrue);
  });

  test('accepts public HTTPS image URLs for Meta while rejecting invalid local refs', () {
    expect(isValidCampaignImageUrl('https://cdn.example.com/images/offer.png'), isTrue);
    expect(isValidCampaignImageUrl('http://localhost:5093/campaign-media/offer.png'), isTrue);
    expect(isValidCampaignImageUrl('campaign-media/offer.png/'), isFalse);
    expect(isValidCampaignImageUrl('../secret.png'), isFalse);
    expect(isValidCampaignImageUrl('campaign-media/../secret.png'), isFalse);
    expect(isValidCampaignImageUrl('https://example.com'), isFalse);
    expect(isValidCampaignImageUrl('ftp://example.com/offer.png'), isFalse);
  });
}
