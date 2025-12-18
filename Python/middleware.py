"""
HTTP middleware for security and performance headers
"""

def add_security_headers(response):
    """
    Add security headers to response
    
    Headers added:
    - Server: Simplified to just app name (security through obscurity)
    - X-Content-Type-Options: Prevent MIME type sniffing
    - X-Frame-Options: Prevent clickjacking
    - X-XSS-Protection: Enable XSS filter
    - Referrer-Policy: Control referrer information
    - Content-Type: Ensure UTF-8 charset
    """
    # Remove detailed server information (security)
    response.headers['Server'] = 'Azure-Speech-App'
    
    # Security headers
    response.headers['X-Content-Type-Options'] = 'nosniff'
    response.headers['X-Frame-Options'] = 'SAMEORIGIN'
    response.headers['X-XSS-Protection'] = '1; mode=block'
    response.headers['Referrer-Policy'] = 'strict-origin-when-cross-origin'
    
    # Ensure Content-Type includes UTF-8 charset
    if response.content_type:
        if 'text/html' in response.content_type and 'charset' not in response.content_type:
            response.headers['Content-Type'] = 'text/html; charset=utf-8'
        elif 'application/json' in response.content_type and 'charset' not in response.content_type:
            response.headers['Content-Type'] = 'application/json; charset=utf-8'
    
    return response


def add_cache_headers(response):
    """
    Add appropriate cache control headers based on content type
    
    Caching strategy:
    - Static resources (CSS, JS, images): Long cache (1 year)
    - HTML pages: No cache (always fresh)
    - API responses: Private, must revalidate
    - File downloads: No cache
    """
    if not response.content_type:
        return response
    
    content_type = response.content_type.lower()
    
    # Static resources - cache for 1 year (with versioning)
    if any(t in content_type for t in ['css', 'javascript', 'image/', 'font']):
        response.headers['Cache-Control'] = 'public, max-age=31536000, immutable'
    
    # HTML pages - no cache (always fetch fresh version)
    elif 'text/html' in content_type:
        response.headers['Cache-Control'] = 'no-cache, no-store, must-revalidate'
        response.headers['Pragma'] = 'no-cache'
        response.headers['Expires'] = '0'
    
    # API responses - private cache, must revalidate
    elif 'application/json' in content_type:
        response.headers['Cache-Control'] = 'private, max-age=0, must-revalidate'
    
    # File downloads - no cache
    elif any(t in content_type for t in ['application/octet-stream', 'application/vnd']):
        response.headers['Cache-Control'] = 'no-cache, no-store, must-revalidate'
    
    # Default - no cache
    else:
        response.headers['Cache-Control'] = 'no-cache, must-revalidate'
    
    return response


def add_cors_headers(response):
    """
    Add CORS headers if needed (optional - uncomment if needed)
    
    Note: Only enable CORS if your app needs to be accessed from other domains
    """
    # Uncomment these lines if you need CORS support:
    # response.headers['Access-Control-Allow-Origin'] = '*'  # Or specific domain
    # response.headers['Access-Control-Allow-Methods'] = 'GET, POST, PUT, DELETE, OPTIONS'
    # response.headers['Access-Control-Allow-Headers'] = 'Content-Type, Authorization'
    
    return response
